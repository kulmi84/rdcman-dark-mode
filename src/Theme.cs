using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Collections.Generic;
using System.Windows.Forms;

namespace RdcManTheme {
 public static class Theme {
  public static readonly Color Background = Color.FromArgb(30,30,30);
  public static readonly Color Panel = Color.FromArgb(37,37,38);
  public static readonly Color Menu = Color.FromArgb(45,45,48);
  public static readonly Color Text = Color.FromArgb(241,241,241);
  public static readonly Color Muted = Color.FromArgb(190,190,190);
  public static readonly Color Selection = Color.FromArgb(62,62,66);
  static readonly Color Border = Color.FromArgb(80,80,84);
  static readonly Color Link = Color.FromArgb(117,190,255);
  public static bool DarkEnabled { get; private set; }
  static bool Switching;
  static readonly List<WeakReference<Control>> Tracked=new List<WeakReference<Control>>();
  sealed class Marker {
   public ChromeWindow Window; public bool Hooked,Applying,Applied,OwnCombo,OwnList;
   public readonly Color Back,Fore; public readonly bool ExplicitBack,ExplicitFore;
   public FlatStyle Flat; public bool Visual; public Color ButtonBorder,Hover,Down;
   public TabDrawMode Tabs; public DrawMode Combo; public bool OwnerDraw;
   public Color LinkColor,ActiveLink,VisitedLink,DisabledLink; public ToolStripRenderer Renderer;
   public ImageList OriginalImages,ModernImages;
   public Marker(Control c) {
    Back=c.BackColor;Fore=c.ForeColor;
    ExplicitBack=TypeDescriptor.GetProperties(c)["BackColor"].ShouldSerializeValue(c);
    ExplicitFore=TypeDescriptor.GetProperties(c)["ForeColor"].ShouldSerializeValue(c);
    var button=c as Button;if(button!=null){Flat=button.FlatStyle;Visual=button.UseVisualStyleBackColor;ButtonBorder=button.FlatAppearance.BorderColor;Hover=button.FlatAppearance.MouseOverBackColor;Down=button.FlatAppearance.MouseDownBackColor;}
    var check=c as CheckBox;if(check!=null){Flat=check.FlatStyle;Visual=check.UseVisualStyleBackColor;}
    var radio=c as RadioButton;if(radio!=null){Flat=radio.FlatStyle;Visual=radio.UseVisualStyleBackColor;}
    var group=c as GroupBox;if(group!=null)Flat=group.FlatStyle;
    var page=c as TabPage;if(page!=null)Visual=page.UseVisualStyleBackColor;
    var tabs=c as TabControl;if(tabs!=null)Tabs=tabs.DrawMode;
    var combo=c as ComboBox;if(combo!=null){Combo=combo.DrawMode;OwnCombo=Combo==DrawMode.Normal;}
    var list=c as ListView;if(list!=null){OwnerDraw=list.OwnerDraw;OwnList=!OwnerDraw;}
    var link=c as LinkLabel;if(link!=null){LinkColor=link.LinkColor;ActiveLink=link.ActiveLinkColor;VisitedLink=link.VisitedLinkColor;DisabledLink=link.DisabledLinkColor;}
    var strip=c as ToolStrip;if(strip!=null)Renderer=strip.Renderer;
    var tree=c as TreeView;if(tree!=null)OriginalImages=tree.ImageList;
   }
  }
  public static void SetDark(bool dark) {
   DarkEnabled=dark;
   Switching=true;
   try {var targets=Tracked.ToArray();
    foreach(var weak in targets){Control c;if(weak.TryGetTarget(out c)&&!c.IsDisposed){Apply(c);c.Invalidate(true);}}
   }finally{Switching=false;}
  }
  static bool Excluded(Control c) { return c==null || c.IsDisposed || c is AxHost || c.GetType().Namespace=="RdcMan.Wrappers"; }
  static void CaptureTree(Control c) {
   if(Excluded(c))return;
   Marker marker;if(!Seen.TryGetValue(c,out marker)){Seen.Add(c,new Marker(c));Tracked.Add(new WeakReference<Control>(c));}
   foreach(Control child in c.Controls)CaptureTree(child);
   if(c.ContextMenuStrip!=null)CaptureTree(c.ContextMenuStrip);
  }
  public static void Observe(Control c) { if(Switching)return;Marker marker;if(!Excluded(c)&&(!Seen.TryGetValue(c,out marker)||!marker.Hooked))Apply(c); }
  static void Restore(Control c,Marker marker) {
   if(!marker.Applied)return;marker.Applied=false;
   if(marker.ExplicitBack)c.BackColor=marker.Back;else c.ResetBackColor();
   if(marker.ExplicitFore)c.ForeColor=marker.Fore;else c.ResetForeColor();
   var button=c as Button;if(button!=null){button.FlatStyle=marker.Flat;button.FlatAppearance.BorderColor=marker.ButtonBorder;button.FlatAppearance.MouseOverBackColor=marker.Hover;button.FlatAppearance.MouseDownBackColor=marker.Down;button.UseVisualStyleBackColor=marker.Visual;}
   var check=c as CheckBox;if(check!=null){check.FlatStyle=marker.Flat;check.UseVisualStyleBackColor=marker.Visual;}
   var radio=c as RadioButton;if(radio!=null){radio.FlatStyle=marker.Flat;radio.UseVisualStyleBackColor=marker.Visual;}
   var group=c as GroupBox;if(group!=null)group.FlatStyle=marker.Flat;
   var page=c as TabPage;if(page!=null)page.UseVisualStyleBackColor=marker.Visual;
   var tabs=c as TabControl;if(tabs!=null)tabs.DrawMode=marker.Tabs;
   var combo=c as ComboBox;if(combo!=null&&marker.OwnCombo)combo.DrawMode=marker.Combo;
   var list=c as ListView;if(list!=null&&marker.OwnList)list.OwnerDraw=marker.OwnerDraw;
   var link=c as LinkLabel;if(link!=null){link.LinkColor=marker.LinkColor;link.ActiveLinkColor=marker.ActiveLink;link.VisitedLinkColor=marker.VisitedLink;link.DisabledLinkColor=marker.DisabledLink;}
   var strip=c as ToolStrip;if(strip!=null)strip.Renderer=marker.Renderer;
   var tree=c as TreeView;if(tree!=null && marker.ModernImages!=null)tree.ImageList=marker.OriginalImages;
  }
  static readonly ConditionalWeakTable<Control,Marker> Seen = new ConditionalWeakTable<Control,Marker>();
  static readonly ConditionalWeakTable<ToolStripItem,ItemMarker> ItemsSeen = new ConditionalWeakTable<ToolStripItem,ItemMarker>();
  static readonly ToolStripRenderer Renderer = new DarkRenderer();
  [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd,int attr,ref int value,int size);
  [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr hwnd,string app,string id);
  [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hwnd,int message,IntPtr wparam,IntPtr lparam);
  [DllImport("user32.dll")] static extern IntPtr GetWindowDC(IntPtr hwnd);
  [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr hwnd,IntPtr dc);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr hwnd,out RECT rect);
  [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left,Top,Right,Bottom; }
  [StructLayout(LayoutKind.Sequential)] struct HDITEM { public uint mask; public int cxy; public IntPtr pszText,hbm; public int cchTextMax,fmt; public IntPtr lParam; public int iImage,iOrder; public uint type; public IntPtr pvFilter; public uint state; }
  [DllImport("user32.dll",EntryPoint="SendMessageW")] static extern IntPtr HeaderItem(IntPtr hwnd,int message,IntPtr index,ref HDITEM item);

  public static void Apply(Control control) {
   if(Excluded(control))return;
   CaptureTree(control);
   Marker marker;
   Seen.TryGetValue(control,out marker);
   if (!marker.Hooked) {
    marker.Hooked=true;
    control.ControlAdded += Added;
    control.HandleCreated += Handle;
    control.ContextMenuStripChanged += ContextChanged;
    control.EnabledChanged += Enabled;
    control.BackColorChanged+=PaletteChanged;control.ForeColorChanged+=PaletteChanged;
    var group=control as GroupBox;
    if(group!=null) { group.Paint += GroupPaint; }
    var tabs=control as TabControl;
    if(tabs!=null) { tabs.DrawItem += TabPaint; }
    var combo=control as ComboBox;
    if(combo!=null && marker.OwnCombo) { combo.DrawItem += ComboPaint; }
    var button=control as Button;
    if(button!=null) button.Paint += DisabledButtonPaint;
    var list=control as ListView;
    if(list!=null && marker.OwnList) { list.DrawColumnHeader+=HeaderPaint; list.DrawItem+=ListItemPaint; list.DrawSubItem+=ListSubItemPaint; }
    if(control.GetType().Name=="UpDownButtons") control.Paint+=SpinnerPaint;
    if(control.GetType().FullName=="RdcMan.ServerTree") control.Disposed+=(sender,args)=>{if(marker.ModernImages!=null)marker.ModernImages.Dispose();};
   }
   Colors(control);
   var strip=control as ToolStrip;
   if(strip!=null) Strip(strip);
   if(control.ContextMenuStrip!=null) Apply(control.ContextMenuStrip);
   if(control.IsHandleCreated) Native(control);
   foreach(Control child in control.Controls) Apply(child);
  }
  static void Colors(Control c) {
   Marker marker;if(!Seen.TryGetValue(c,out marker)||marker.Applying)return;
   marker.Applying=true;
   try {
   if(!DarkEnabled){Restore(c,marker);return;}marker.Applied=true;
   var group=c as GroupBox;if(group!=null)group.FlatStyle=FlatStyle.Flat;
   var tabs=c as TabControl;if(tabs!=null)tabs.DrawMode=TabDrawMode.OwnerDrawFixed;
   var combo=c as ComboBox;if(combo!=null&&marker.OwnCombo)combo.DrawMode=DrawMode.OwnerDrawFixed;
   var lv=c as ListView;if(lv!=null&&marker.OwnList)lv.OwnerDraw=true;
   bool baseSurface=c is Form || c is TreeView || c.GetType().FullName=="RdcMan.ClientPanel";
   c.BackColor=c is ToolStrip ? Menu : baseSurface ? Background : Panel;
   c.ForeColor=c.Enabled ? Text : Muted;
   if(c.GetType().FullName=="RdcMan.ServerTree"){c.ForeColor=c.Focused?Text:Muted;FolderIcons((TreeView)c,marker);}
   if(c.GetType().FullName=="RdcMan.ServerLabel"){c.BackColor=c.Focused?Selection:Panel;c.ForeColor=c.Focused?Text:Muted;}
   var b=c as Button;
   if(b!=null) { b.UseVisualStyleBackColor=false; b.FlatStyle=FlatStyle.Flat; b.FlatAppearance.BorderColor=Border; b.FlatAppearance.MouseOverBackColor=Selection; b.FlatAppearance.MouseDownBackColor=Menu; }
   var cb=c as CheckBox;
   if(cb!=null) { cb.UseVisualStyleBackColor=false; cb.FlatStyle=FlatStyle.Flat; }
   var rb=c as RadioButton;
   if(rb!=null) { rb.UseVisualStyleBackColor=false; rb.FlatStyle=FlatStyle.Flat; }
   var page=c as TabPage;
   if(page!=null) page.UseVisualStyleBackColor=false;
   var link=c as LinkLabel;
   if(link!=null) { link.LinkColor=Link; link.ActiveLinkColor=Text; link.VisitedLinkColor=Link; link.DisabledLinkColor=Muted; }
   } finally {marker.Applying=false;}
  }
  static void FolderIcons(TreeView tree,Marker marker) {
   // RDCMan 3.12: 0..4 are connection states, 5 group, 6 smartgroup,
   // 7 default. Preserve indexes, keys and all non-folder images.
   if(marker.OriginalImages==null && tree.ImageList!=null)marker.OriginalImages=tree.ImageList;
   if(marker.OriginalImages==null || marker.OriginalImages.Images.Count<8)return;
   if(marker.ModernImages==null){
    var original=marker.OriginalImages;
    var modern=new ImageList{ColorDepth=ColorDepth.Depth32Bit,ImageSize=original.ImageSize,TransparentColor=original.TransparentColor};
    var copies=new List<Bitmap>();
    for(int i=0;i<original.Images.Count;i++){
     var image=i==5 || i==6?FolderImage(original.ImageSize,i==6):new Bitmap(original.Images[i]);copies.Add(image);modern.Images.Add(original.Images.Keys[i],image);
    }
    var handle=modern.Handle;foreach(var image in copies)image.Dispose();
    marker.ModernImages=modern;
   }
   if(tree.ImageList!=marker.ModernImages)tree.ImageList=marker.ModernImages;
  }
  static Bitmap FolderImage(Size size,bool smart) {
   var image=new Bitmap(size.Width,size.Height);
   using(var g=Graphics.FromImage(image)){
    g.Clear(Color.Transparent);g.ScaleTransform(size.Width/20f,size.Height/20f);
    g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
    using(var pen=new Pen(Text,1.55f)){
     pen.StartCap=pen.EndCap=System.Drawing.Drawing2D.LineCap.Round;pen.LineJoin=System.Drawing.Drawing2D.LineJoin.Round;
     // Same original vector path as KeeTheme's modern folder glyph.
     g.DrawLines(pen,new[]{new PointF(3,16),new PointF(3,5),new PointF(8,5),new PointF(10,7),new PointF(17,7)});
     g.DrawPolygon(pen,new[]{new PointF(3,16),new PointF(6,9),new PointF(18,9),new PointF(15,16)});
    }
    if(smart)using(var brush=new SolidBrush(Link))g.FillEllipse(brush,13,13,5,5);
   }
   return image;
  }
  static void PaletteChanged(object sender,EventArgs e) { if(DarkEnabled&&!Switching)Colors((Control)sender);

  }
  static void Added(object sender,ControlEventArgs e) { if(Switching)return; Apply(e.Control); }
  static void Handle(object sender,EventArgs e) { if(Switching)return; var c=(Control)sender; Colors(c); Native(c); }
  static void ContextChanged(object sender,EventArgs e) { var c=(Control)sender; if(c.ContextMenuStrip!=null) Apply(c.ContextMenuStrip); }
  static void Enabled(object sender,EventArgs e) { if(Switching)return; Colors((Control)sender); }
  static void Native(Control c) {
   if(c is Form) { int yes=DarkEnabled?1:0; try { if(DwmSetWindowAttribute(c.Handle,20,ref yes,4)!=0) DwmSetWindowAttribute(c.Handle,19,ref yes,4); } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {} }
   bool chrome=c is TabControl || c is TreeView || c is ListView || c is NumericUpDown || (c is TextBoxBase && ((TextBoxBase)c).BorderStyle!=BorderStyle.None);
   if(chrome && DarkEnabled) {
    Marker marker;
    if(Seen.TryGetValue(c,out marker) && marker.Window==null) { marker.Window=new ChromeWindow(c); }
   }
   if(c is TreeView || c is ListView || c is TextBoxBase || c is ComboBox || c is ScrollBar || c is NumericUpDown) {
    try {
     // EDIT's themed hover transition can paint a bright border after WM_NCPAINT.
     // Disable it only for single-line fields. Multiline EDIT controls need
     // DarkMode_Explorer to theme their native scrollbars.
     bool plainEdit=c is TextBoxBase && !((TextBoxBase)c).Multiline;
     SetWindowTheme(c.Handle,DarkEnabled ? (plainEdit ? "" : c is ComboBox ? "DarkMode_CFD" : "DarkMode_Explorer") : null,DarkEnabled && plainEdit ? "" : null);
     if(c is ListView) { var header=SendMessage(c.Handle,0x101F,IntPtr.Zero,IntPtr.Zero); if(header!=IntPtr.Zero) SetWindowTheme(header,DarkEnabled?"DarkMode_ItemsView":null,null); }
    } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
   }
  }
  static void Strip(ToolStrip s) {
   if(DarkEnabled){s.Renderer=Renderer;s.BackColor=Menu;s.ForeColor=Text;}
   Marker stripMarker;
   if(!StripSeen.TryGetValue(s,out stripMarker)) { StripSeen.Add(s,new Marker(s)); s.ItemAdded += ItemAdded; }
   foreach(ToolStripItem item in s.Items) Item(item);
   var drop=s as ToolStripDropDown;
   Marker marker;
   // Separate event marker on the strip's Tag is deliberately avoided: RDCMan may use Tag.
   if(drop!=null && !DropSeen.TryGetValue(drop,out marker)) { DropSeen.Add(drop,new Marker(drop)); drop.Opening += Opening; }
  }
  static readonly ConditionalWeakTable<ToolStrip,Marker> StripSeen=new ConditionalWeakTable<ToolStrip,Marker>();
  static void ItemAdded(object sender,ToolStripItemEventArgs e) { Item(e.Item); }
  static readonly ConditionalWeakTable<ToolStripDropDown,Marker> DropSeen=new ConditionalWeakTable<ToolStripDropDown,Marker>();
  static void Opening(object sender,System.ComponentModel.CancelEventArgs e) { Strip((ToolStrip)sender); }
  sealed class ItemMarker {public Color Back,Fore;public bool ExplicitBack,ExplicitFore;public ItemMarker(ToolStripItem item){Back=item.BackColor;Fore=item.ForeColor;ExplicitBack=TypeDescriptor.GetProperties(item)["BackColor"].ShouldSerializeValue(item);ExplicitFore=TypeDescriptor.GetProperties(item)["ForeColor"].ShouldSerializeValue(item);}}
  static void Item(ToolStripItem item) {
   ItemMarker marker;bool first=!ItemsSeen.TryGetValue(item,out marker);
   if(first){marker=new ItemMarker(item);ItemsSeen.Add(item,marker);}
   if(DarkEnabled){item.BackColor=Menu;item.ForeColor=item.Enabled?Text:Muted;}
   else {if(marker.ExplicitBack)item.BackColor=marker.Back;else TypeDescriptor.GetProperties(item)["BackColor"].ResetValue(item);if(marker.ExplicitFore)item.ForeColor=marker.Fore;else TypeDescriptor.GetProperties(item)["ForeColor"].ResetValue(item);}
   var drop=item as ToolStripDropDownItem;
   if(drop!=null) {
    if(first) { drop.DropDownOpening += DropOpening; }
    if(drop.HasDropDownItems) Apply(drop.DropDown);
   }
  }
  static void DropOpening(object sender,EventArgs e) { Apply(((ToolStripDropDownItem)sender).DropDown); }
  public static void ApplyMenu(ToolStrip s) { Apply(s); }
  static void TabPaint(object sender,DrawItemEventArgs e) {
   if(!DarkEnabled)return;
   var tabs=(TabControl)sender; if(e.Index<0 || e.Index>=tabs.TabCount) return;
   var page=tabs.TabPages[e.Index]; bool selected=e.Index==tabs.SelectedIndex;
   using(var brush=new SolidBrush(selected ? Selection : Panel)) e.Graphics.FillRectangle(brush,e.Bounds);
   using(var pen=new Pen(Border)) e.Graphics.DrawRectangle(pen,e.Bounds.Left,e.Bounds.Top,Math.Max(0,e.Bounds.Width-1),Math.Max(0,e.Bounds.Height-1));
   TextRenderer.DrawText(e.Graphics,page.Text,tabs.Font,e.Bounds,page.Enabled ? Text : Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
   if(selected) using(var pen=new Pen(Link,2)) e.Graphics.DrawLine(pen,e.Bounds.Left+3,e.Bounds.Bottom-2,e.Bounds.Right-3,e.Bounds.Bottom-2);
  }
  static void ComboPaint(object sender,DrawItemEventArgs e) {
   if(!DarkEnabled)return;
   var combo=(ComboBox)sender; bool selected=(e.State&DrawItemState.Selected)!=0;
   using(var brush=new SolidBrush(selected ? Selection : Panel)) e.Graphics.FillRectangle(brush,e.Bounds);
   string value=e.Index>=0 && e.Index<combo.Items.Count ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
   TextRenderer.DrawText(e.Graphics,value,combo.Font,e.Bounds,combo.Enabled ? Text : Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
   e.DrawFocusRectangle();
  }
  static void GroupPaint(object sender,PaintEventArgs e) {
   if(!DarkEnabled)return;
   var g=(GroupBox)sender; e.Graphics.Clear(Panel);
   int y=Math.Max(7,g.Font.Height/2); var rect=new Rectangle(0,y,Math.Max(0,g.Width-1),Math.Max(0,g.Height-y-1));
   using(var pen=new Pen(Border)) e.Graphics.DrawRectangle(pen,rect);
   var size=TextRenderer.MeasureText(e.Graphics,g.Text,g.Font);
   using(var brush=new SolidBrush(Panel)) e.Graphics.FillRectangle(brush,6,0,size.Width+4,size.Height);
   TextRenderer.DrawText(e.Graphics,g.Text,g.Font,new Point(8,0),g.Enabled ? Text : Muted,Panel);
  }
  static void DisabledButtonPaint(object sender,PaintEventArgs e) {
   if(!DarkEnabled)return;
   var button=(Button)sender;if(button.Enabled)return;
   e.Graphics.Clear(Panel);
   using(var pen=new Pen(Border)) e.Graphics.DrawRectangle(pen,0,0,Math.Max(0,button.Width-1),Math.Max(0,button.Height-1));
   TextRenderer.DrawText(e.Graphics,button.Text,button.Font,button.ClientRectangle,Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  }
  static void SpinnerPaint(object sender,PaintEventArgs e) {
   if(!DarkEnabled)return;
   var c=(Control)sender;var r=c.ClientRectangle;e.Graphics.Clear(Panel);
   using(var pen=new Pen(Border)) { e.Graphics.DrawRectangle(pen,0,0,Math.Max(0,r.Width-1),Math.Max(0,r.Height-1));e.Graphics.DrawLine(pen,0,r.Height/2,r.Width,r.Height/2); }
   using(var brush=new SolidBrush(c.Enabled ? Text : Muted)) {
    int x=r.Width/2,y=r.Height/4;
    e.Graphics.FillPolygon(brush,new[]{new Point(x-3,y+2),new Point(x+3,y+2),new Point(x,y-2)});
    y=r.Height*3/4;e.Graphics.FillPolygon(brush,new[]{new Point(x-3,y-2),new Point(x+3,y-2),new Point(x,y+2)});
   }
  }
  static void ListItemPaint(object sender,DrawListViewItemEventArgs e) { e.DrawDefault=true; }
  static void ListSubItemPaint(object sender,DrawListViewSubItemEventArgs e) { e.DrawDefault=true; }
  static void HeaderPaint(object sender,DrawListViewColumnHeaderEventArgs e) {
   if(!DarkEnabled){e.DrawDefault=true;return;}
   var list=(ListView)sender;using(var brush=new SolidBrush(Menu)) e.Graphics.FillRectangle(brush,e.Bounds);
   using(var pen=new Pen(Border)) { e.Graphics.DrawLine(pen,e.Bounds.Right-1,e.Bounds.Top,e.Bounds.Right-1,e.Bounds.Bottom);e.Graphics.DrawLine(pen,e.Bounds.Left,e.Bounds.Bottom-1,e.Bounds.Right,e.Bounds.Bottom-1); }
   var rect=e.Bounds;rect.Inflate(-6,0);
   int format=0;
   var header=SendMessage(list.Handle,0x101F,IntPtr.Zero,IntPtr.Zero);
   var item=new HDITEM{mask=4};if(header!=IntPtr.Zero && HeaderItem(header,0x120B,new IntPtr(e.ColumnIndex),ref item)!=IntPtr.Zero)format=item.fmt;
   // RDCMan's native header checkbox hit-testing and change handlers remain intact.
   if((format&0x40)!=0) {
    int y=e.Bounds.Top+(e.Bounds.Height-12)/2;var check=new Rectangle(rect.Left,y,12,12);
    using(var pen=new Pen(Muted)) e.Graphics.DrawRectangle(pen,check);
    if((format&0x80)!=0) using(var pen=new Pen(Text,2)) e.Graphics.DrawLines(pen,new[]{new Point(check.Left+2,check.Top+6),new Point(check.Left+5,check.Bottom-3),new Point(check.Right-2,check.Top+3)});
    rect.X+=18;rect.Width=Math.Max(0,rect.Width-18);
   }
   var flags=TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis;
   flags|=e.Header.TextAlign==HorizontalAlignment.Right ? TextFormatFlags.Right : e.Header.TextAlign==HorizontalAlignment.Center ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left;
   TextRenderer.DrawText(e.Graphics,e.Header.Text,list.Font,rect,list.Enabled ? Text : Muted,flags);
   if((format&0x600)!=0) {
    int x=e.Bounds.Right-10,y=e.Bounds.Top+e.Bounds.Height/2;bool up=(format&0x400)!=0;
    using(var brush=new SolidBrush(Text)) e.Graphics.FillPolygon(brush,up ? new[]{new Point(x-3,y+2),new Point(x+3,y+2),new Point(x,y-2)} : new[]{new Point(x-3,y-2),new Point(x+3,y-2),new Point(x,y+2)});
   }
  }
  sealed class ChromeWindow : NativeWindow {
   readonly Control Control;
   public ChromeWindow(Control control) { Control=control;AssignHandle(control.Handle);control.HandleDestroyed+=Destroyed;control.HandleCreated+=Created; }
   void Destroyed(object sender,EventArgs e) { ReleaseHandle(); }
   void Created(object sender,EventArgs e) { if(Handle==IntPtr.Zero && !Control.IsDisposed)AssignHandle(Control.Handle); }
   protected override void WndProc(ref Message m) {
    // Paint simple EDIT frames directly, without first exposing the native frame.
    // Multiline scrolling fields retain native nonclient scrollbar processing.
    var text=Control as TextBox;
    if(DarkEnabled && !Control.IsDisposed && m.Msg==0x85 && Control is TextBoxBase && (text==null || text.ScrollBars==ScrollBars.None)) {
     PaintWindowBorder();m.Result=IntPtr.Zero;return;
    }
    base.WndProc(ref m);
    if(Control.IsDisposed || !DarkEnabled)return;
    if(m.Msg==0xF && Control is TabControl) using(var g=Graphics.FromHwnd(Handle)) Tabs(g);
    if((m.Msg==0x317 || m.Msg==0x318) && m.WParam!=IntPtr.Zero) using(var g=Graphics.FromHdc(m.WParam)) {
     if(Control is TabControl)Tabs(g);
     if(m.Msg==0x317)BorderPaint(g,Control.Width,Control.Height);
    }
    if(m.Msg==0x85) PaintWindowBorder();
   }
   void PaintWindowBorder() {
     var dc=GetWindowDC(Handle);
     if(dc!=IntPtr.Zero) { try { RECT rect;GetWindowRect(Handle,out rect);using(var g=Graphics.FromHdc(dc))BorderPaint(g,rect.Right-rect.Left,rect.Bottom-rect.Top); } finally {ReleaseDC(Handle,dc);} }
   }
   void BorderPaint(Graphics g,int width,int height) { if(width<4 || height<4)return;using(var pen=new Pen(Border)) {g.DrawRectangle(pen,0,0,width-1,height-1);g.DrawRectangle(pen,1,1,width-3,height-3);} }
   void Tabs(Graphics g) {
    var tabs=(TabControl)Control;
    using(var region=new Region(tabs.ClientRectangle)) {
     region.Exclude(tabs.DisplayRectangle);
     for(int i=0;i<tabs.TabCount;i++)region.Exclude(tabs.GetTabRect(i));
     using(var brush=new SolidBrush(Panel))g.FillRegion(brush,region);
    }
    var d=tabs.DisplayRectangle;using(var pen=new Pen(Border))g.DrawRectangle(pen,d.Left-1,d.Top-1,d.Width+1,d.Height+1);
    // Native tab chrome paints bright bevels outside DrawItem's inner bounds.
    // Repaint the complete tab rectangles after native painting, including
    // every row of a multiline TabControl, using the same gray border.
    for(int i=0;i<tabs.TabCount;i++) {
     var bounds=tabs.GetTabRect(i);
     TabPaint(tabs,new DrawItemEventArgs(g,tabs.Font,bounds,i,i==tabs.SelectedIndex?DrawItemState.Selected:DrawItemState.None));
    }
   }
  }
  sealed class DarkColors : ProfessionalColorTable {
   public override Color ToolStripDropDownBackground {get{return Menu;}}
   public override Color MenuStripGradientBegin {get{return Menu;}}
   public override Color MenuStripGradientEnd {get{return Menu;}}
   public override Color ImageMarginGradientBegin {get{return Menu;}}
   public override Color ImageMarginGradientMiddle {get{return Menu;}}
   public override Color ImageMarginGradientEnd {get{return Menu;}}
   public override Color MenuItemSelected {get{return Selection;}}
   public override Color MenuItemSelectedGradientBegin {get{return Selection;}}
   public override Color MenuItemSelectedGradientEnd {get{return Selection;}}
   public override Color MenuItemPressedGradientBegin {get{return Selection;}}
   public override Color MenuItemPressedGradientMiddle {get{return Selection;}}
   public override Color MenuItemPressedGradientEnd {get{return Selection;}}
   public override Color MenuItemBorder {get{return Border;}}
   public override Color MenuBorder {get{return Border;}}
   public override Color SeparatorDark {get{return Border;}}
   public override Color SeparatorLight {get{return Menu;}}
   public override Color CheckBackground {get{return Selection;}}
   public override Color CheckSelectedBackground {get{return Selection;}}
   public override Color CheckPressedBackground {get{return Selection;}}
  }
  sealed class DarkRenderer : ToolStripProfessionalRenderer {
   public DarkRenderer():base(new DarkColors()) { RoundedEdges=false; }
   protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) { e.TextColor=e.Item.Enabled ? Text : Muted; base.OnRenderItemText(e); }
   protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) { e.ArrowColor=e.Item.Enabled ? Text : Muted; base.OnRenderArrow(e); }
   protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
    using(var pen=new Pen(e.Item.Enabled ? Text : Muted,2)) {
     var r=e.ImageRectangle; e.Graphics.DrawLines(pen,new[]{new Point(r.Left+2,r.Top+r.Height/2),new Point(r.Left+r.Width/2-1,r.Bottom-3),new Point(r.Right-2,r.Top+3)});
    }
   }
  }
 }
}
