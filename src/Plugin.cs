using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using Microsoft.Win32;
using RdcMan;
using RDCMan.Nodes;
namespace RdcManTheme {
 public enum ThemeMode { Light,Dark,System }
 [Export(typeof(IPlugin))]
 public sealed class ThemePlugin : IPlugin,IMessageFilter {
  IPluginContext context;
  ToolStripMenuItem menu,light,dark,system;
  Timer timer;
  ThemeMode mode=ThemeMode.Dark;
  bool forget,started,stopped,observing;
  IntPtr dialogHook;
  HookProc dialogCallback;
  bool preparingDialog;
  readonly HashSet<Form> confirmingDialogs=new HashSet<Form>();
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
  readonly ConditionalWeakTable<Form,OpeningDialog> openingDialogs=new ConditionalWeakTable<Form,OpeningDialog>();
  sealed class OpeningDialog {public double Opacity;public bool Revealed;}
  [StructLayout(LayoutKind.Sequential)] struct CWPSTRUCT {public IntPtr LParam,WParam;public uint Message;public IntPtr Window;}
  [StructLayout(LayoutKind.Sequential)] struct WINDOWPOS {public IntPtr Window,After;public int X,Y,Width,Height;public uint Flags;}
  delegate IntPtr HookProc(int code,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll",SetLastError=true)] static extern IntPtr SetWindowsHookEx(int id,HookProc callback,IntPtr module,uint thread);
  [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wParam,IntPtr lParam);
  [DllImport("user32.dll")] static extern bool UnhookWindowsHookEx(IntPtr hook);
  [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
  [DllImport("user32.dll")] static extern bool RedrawWindow(IntPtr window,IntPtr rectangle,IntPtr region,uint flags);
  internal static Func<bool> WindowsThemeReader=WindowsDark;
  public ThemeMode Mode {get{return mode;}}
  // Optional startup-patched EXE calls this before exposing MainForm.
  // Capture the original palette before applying the saved mode; normal MEF
  // PreLoad/PostLoad still own the plugin instance and its complete lifecycle.
  public static void PrepareFirstFrame(Control form,XmlNode settings) {
   var stored=ThemeMode.Dark;
   var node=settings==null?null:settings.SelectSingleNode("//plugin[@path='Plugin.RDCManTheme']/theme");
   ThemeMode parsed;
   if(node!=null && node.Attributes["mode"]!=null && Enum.TryParse(node.Attributes["mode"].Value,out parsed) && Enum.IsDefined(typeof(ThemeMode),parsed))stored=parsed;
   Theme.SetDark(stored==ThemeMode.Dark || (stored==ThemeMode.System && WindowsThemeReader()));
   Theme.Apply(form);
  }
  public void PreLoad(IPluginContext pluginContext,XmlNode settings) {
   context=pluginContext;
   var node=settings==null?null:settings.SelectSingleNode("theme");
   ThemeMode stored;
   if(node!=null && node.Attributes["mode"]!=null && Enum.TryParse(node.Attributes["mode"].Value,out stored) && Enum.IsDefined(typeof(ThemeMode),stored))mode=stored;
   // PreLoad runs before RDCMan opens its saved server files. Apply the saved
   // mode here so file loading and newly added controls already use the theme.
   BeginObserving();SetMode(mode);
  }
  public void PostLoad(IPluginContext pluginContext) {
   if(started)return;started=true;context=pluginContext;
   menu=new ToolStripMenuItem("Darstellung");
   light=new ToolStripMenuItem("Hell",null,(s,e)=>SetMode(ThemeMode.Light));
   dark=new ToolStripMenuItem("Dunkel",null,(s,e)=>SetMode(ThemeMode.Dark));
   system=new ToolStripMenuItem("Windows-Einstellung",null,(s,e)=>SetMode(ThemeMode.System));
   menu.DropDownItems.AddRange(new ToolStripItem[]{light,dark,system,new ToolStripSeparator(),new ToolStripMenuItem("Für Deinstallation deaktivieren",null,DisableForRemoval)});
   context.MainForm.MainMenuStrip.Items.Add(menu);
   BeginObserving();
   ObserveForms();SetMode(mode);
  }
  void BeginObserving() {
   if(observing || stopped)return;observing=true;
   dialogCallback=BeforeDialogActivation;
   dialogHook=SetWindowsHookEx(4,dialogCallback,IntPtr.Zero,GetCurrentThreadId());
   if(dialogHook==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   Application.AddMessageFilter(this);Application.Idle+=Idle;
   timer=new Timer{Interval=500};timer.Tick+=Tick;timer.Start();
  }
  IntPtr BeforeDialogActivation(int code,IntPtr window,IntPtr data) {
   // CALLWNDPROC sees synchronous messages before visibility, earlier than
   // CBT activation. Do not depend on queued WM_PAINT or Application.Idle.
   if(code>=0 && !stopped && !preparingDialog) {
    try {
     var message=Marshal.PtrToStructure<CWPSTRUCT>(data);
     if(message.Message==0x111 && message.LParam!=IntPtr.Zero && ((message.WParam.ToInt64()>>16)&0xffff)==0) {
      var dialog=Control.FromHandle(message.Window) as Form;
      if(dialog!=null && Theme.DarkEnabled && !confirmingDialogs.Contains(dialog)) {
       var type=dialog.GetType();System.Reflection.FieldInfo accept=null;
       while(type!=null && accept==null){accept=type.GetField("_acceptButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);type=type.BaseType;}
       var button=accept==null?null:accept.GetValue(dialog) as Control;
       if(button!=null && button.IsHandleCreated && button.Handle==message.LParam)BatchConfirmation(dialog);
      }
     }
     bool showing=message.Message==0x18 && message.WParam!=IntPtr.Zero;
     if(message.Message==0x46 && message.LParam!=IntPtr.Zero)showing=(Marshal.PtrToStructure<WINDOWPOS>(message.LParam).Flags&0x40)!=0;
     var form=showing?Control.FromHandle(message.Window) as Form:null;
     if(form!=null){preparingDialog=true;try{PrepareDialog(form);Theme.Apply(form);}finally{preparingDialog=false;}}
    }
    // Never propagate a managed exception across the native callback boundary.
    catch(Exception ex){System.Diagnostics.Trace.WriteLine("RDCMan theme dialog hook: "+ex);}
   }
   return CallNextHookEx(dialogHook,code,window,data);
  }
  void BatchConfirmation(Form dialog) {
   double opacity=dialog.Opacity;
   var windows=new List<Control>();
   Action<Control> collect=null;collect=c=>{if(!(c is Form) && c.IsHandleCreated && c.Visible)windows.Add(c);foreach(Control child in c.Controls)collect(child);};collect(dialog);
   confirmingDialogs.Add(dialog);
   // Validation can switch/resize native tabs and paint synchronously even
   // with child redraw disabled. Keep the closing dialog off the compositor
   // until validation has returned; restore it if validation keeps it open.
   dialog.Opacity=0;
   foreach(var c in windows){Theme.SuspendPainting(c,true);SendMessage(c.Handle,0xB,IntPtr.Zero,IntPtr.Zero);}
   var dispatcher=context==null?null:context.MainForm as Control;
   try {
    (dispatcher??dialog).BeginInvoke(new Action(()=>{
     try {
      foreach(var c in windows){Theme.SuspendPainting(c,false);if(!c.IsDisposed && c.IsHandleCreated)SendMessage(c.Handle,0xB,new IntPtr(1),IntPtr.Zero);}
      if(!dialog.IsDisposed && dialog.Visible){Theme.Apply(dialog);RedrawWindow(dialog.Handle,IntPtr.Zero,IntPtr.Zero,0x585);}
     }finally{if(!dialog.IsDisposed)dialog.Opacity=opacity;confirmingDialogs.Remove(dialog);}
    }));
   }catch{
    foreach(var c in windows){Theme.SuspendPainting(c,false);if(!c.IsDisposed && c.IsHandleCreated)SendMessage(c.Handle,0xB,new IntPtr(1),IntPtr.Zero);}
    if(!dialog.IsDisposed)dialog.Opacity=opacity;confirmingDialogs.Remove(dialog);throw;
   }
  }
  void PrepareDialog(Form form) {
   // RDCMan builds and focuses its settings tabs in ShownCallback. Keep only
   // those newly opening dialogs transparent until that callback has finished.
   if(!Theme.DarkEnabled || form.GetType().Namespace==null || !form.GetType().Namespace.StartsWith("RDCMan."))return;
   OpeningDialog state;if(openingDialogs.TryGetValue(form,out state))return;
   state=new OpeningDialog{Opacity=form.Opacity};openingDialogs.Add(form,state);
   EventHandler reveal=null;
   reveal=(sender,args)=>{
    form.Shown-=reveal;
    form.BeginInvoke(new Action(()=>{
     if(form.IsDisposed)return;
     try{
      if(!stopped)Theme.Apply(form);
      // Commit child and nonclient paint while still transparent. Merely
      // invalidating after restoring opacity exposed old white button pixels.
      form.Refresh();
      RedrawWindow(form.Handle,IntPtr.Zero,IntPtr.Zero,0x585); // INVALIDATE|ERASE|ALLCHILDREN|UPDATENOW|FRAME
     }
     finally{state.Revealed=true;form.Opacity=state.Opacity;}
    }));
   };
   form.Shown+=reveal;
   try{form.Opacity=0;}catch{form.Shown-=reveal;openingDialogs.Remove(form);throw;}
  }
  public void SetMode(ThemeMode value) {
   mode=value;forget=false;
   bool useDark=value==ThemeMode.Dark || (value==ThemeMode.System && WindowsThemeReader());
   Theme.SetDark(useDark);
   if(menu!=null){light.Checked=value==ThemeMode.Light;dark.Checked=value==ThemeMode.Dark;system.Checked=value==ThemeMode.System;}
   ObserveForms();
  }
  static bool WindowsDark() {
   try { var value=Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","AppsUseLightTheme",1);return Convert.ToInt32(value)==0; }
   catch(System.Security.SecurityException){return false;}catch(UnauthorizedAccessException){return false;}catch(System.IO.IOException){return false;}
  }
  void Tick(object sender,EventArgs e) {
   if(stopped)return;
   if(mode==ThemeMode.System){bool useDark=WindowsThemeReader();if(useDark!=Theme.DarkEnabled)Theme.SetDark(useDark);}
   ObserveForms();
  }
  void Idle(object sender,EventArgs e) {if(!stopped)ObserveForms();}
  void ObserveForms() {
   if(context!=null && context.MainForm is Control)Theme.Observe((Control)context.MainForm);
   var forms=new List<Form>();foreach(Form form in Application.OpenForms)forms.Add(form);
   foreach(var form in forms)Theme.Observe(form);
  }
  public bool PreFilterMessage(ref Message message) {
   // WinForms Button.OnMouseUp can call OnClick without a native BN_CLICKED.
   // Start the paint batch before dispatching mouse/keyboard confirmation.
   if(!stopped && Theme.DarkEnabled && (message.Msg==0x202 || message.Msg==0x100 || message.Msg==0x101)) {
    var control=Control.FromHandle(message.HWnd);
    var dialog=control==null?null:control.FindForm();
    if(dialog!=null && !confirmingDialogs.Contains(dialog)) {
     var type=dialog.GetType();System.Reflection.FieldInfo field=null;
     while(type!=null && field==null){field=type.GetField("_acceptButton",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.DeclaredOnly);type=type.BaseType;}
     var accept=field==null?null:field.GetValue(dialog) as Control;
     var key=(Keys)message.WParam.ToInt32();
     bool mouse=message.Msg==0x202 && control==accept;
     bool keyboard=(message.Msg==0x101 && key==Keys.Space && control==accept) || (message.Msg==0x100 && key==Keys.Enter && !(control is TextBoxBase && ((TextBoxBase)control).Multiline));
     if(accept!=null && accept.Enabled && (mouse || keyboard))BatchConfirmation(dialog);
    }
   }
   if(!stopped && message.Msg==0xF){var c=Control.FromHandle(message.HWnd);if(c!=null && !(c is AxHost)){var form=c.FindForm();if(form!=null)Theme.Observe(form);}}
   return false;
  }
  void DisableForRemoval(object sender,EventArgs e) {
   SetMode(ThemeMode.Light);forget=true;
   MessageBox.Show((context.MainForm as Control),"RDCMan jetzt schließen, danach Plugin.RDCManTheme.dll aus dem Programmordner entfernen. Die Plugin-Einstellung wird beim regulären Beenden nicht mehr gespeichert.","Theme entfernen",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  public XmlNode SaveSettings() {
   if(forget)return null;
   var doc=new XmlDocument();var element=doc.CreateElement("theme");element.SetAttribute("mode",mode.ToString());return element;
  }
  public void Shutdown() {
   if(stopped)return;stopped=true;
   if(dialogHook!=IntPtr.Zero){UnhookWindowsHookEx(dialogHook);dialogHook=IntPtr.Zero;}
   if(timer!=null){timer.Stop();timer.Dispose();}
   Application.Idle-=Idle;Application.RemoveMessageFilter(this);
   Theme.SetDark(false);
   if(menu!=null){if(menu.Owner!=null)menu.Owner.Items.Remove(menu);menu.Dispose();}
  }
  public void OnContextMenu(ContextMenuStrip strip,RdcTreeNode node) {Theme.Apply(strip);}
  public void OnUndockServer(IUndockedServerForm form) {var control=form as Control;if(control!=null)Theme.Apply(control);}
  public void OnDockServer(ServerBase server) {ObserveForms();}
 }
}




