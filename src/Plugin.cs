using System;
using System.ComponentModel.Composition;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
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
  bool forget,started,stopped;
  internal static Func<bool> WindowsThemeReader=WindowsDark;
  public ThemeMode Mode {get{return mode;}}
  public void PreLoad(IPluginContext pluginContext,XmlNode settings) {
   context=pluginContext;
   var node=settings==null?null:settings.SelectSingleNode("theme");
   ThemeMode stored;
   if(node!=null && node.Attributes["mode"]!=null && Enum.TryParse(node.Attributes["mode"].Value,out stored) && Enum.IsDefined(typeof(ThemeMode),stored))mode=stored;
  }
  public void PostLoad(IPluginContext pluginContext) {
   if(started)return;started=true;context=pluginContext;
   menu=new ToolStripMenuItem("Darstellung");
   light=new ToolStripMenuItem("Hell",null,(s,e)=>SetMode(ThemeMode.Light));
   dark=new ToolStripMenuItem("Dunkel",null,(s,e)=>SetMode(ThemeMode.Dark));
   system=new ToolStripMenuItem("Windows-Einstellung",null,(s,e)=>SetMode(ThemeMode.System));
   menu.DropDownItems.AddRange(new ToolStripItem[]{light,dark,system,new ToolStripSeparator(),new ToolStripMenuItem("Für Deinstallation deaktivieren",null,DisableForRemoval)});
   context.MainForm.MainMenuStrip.Items.Add(menu);
   Application.AddMessageFilter(this);Application.Idle+=Idle;
   timer=new Timer{Interval=500};timer.Tick+=Tick;timer.Start();
   ObserveForms();SetMode(mode);
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
