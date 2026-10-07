using System;
using System.IO;
using System.Drawing;
using System.Linq;
using System.ComponentModel.Composition.Hosting;
using System.Windows.Forms;
using System.Xml;
using RdcMan;
using RdcManTheme;
public class HostForm : Form,IMainForm {
 public HostForm(){MainMenuStrip=new MenuStrip();Controls.Add(MainMenuStrip);ClientSize=new Size(900,600);ShowInTaskbar=false;Location=new Point(-20000,-20000);StartPosition=FormStartPosition.Manual;Text="RDCMan Theme-Plugin – isolierter Test";}
 MenuStrip IMainForm.MainMenuStrip{get{return MainMenuStrip;}}
 public bool RegisterShortcut(Keys keys,Action action){return true;}
}
public class Context : IPluginContext {
 public IMainForm MainForm {get;set;}
 public IServerTree Tree{get{return null;}}
}
public sealed class FakeAxHost : AxHost {public FakeAxHost():base("00000000-0000-0000-0000-000000000000") {}}
namespace RdcMan {
 // This synthetic class only simulates RDCMan's focus-driven color resets.
 public class ServerTree : TreeView {public void SimulateLegacyPalette(){BackColor=Color.White;ForeColor=Color.Black;}}
}
public static class PluginTest {
 static void Assert(bool value,string msg){if(!value)throw new Exception(msg);}
 static string root;
 static void Image(Form f,string name){using(var image=new Bitmap(f.Width,f.Height)){f.DrawToBitmap(image,new Rectangle(0,0,f.Width,f.Height));image.Save(Path.Combine(root,name));}}
 [STAThread]public static void Main(){
  root=AppContext.BaseDirectory;
  try{
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   using(var first=new Form()){
    var back=first.BackColor;
    var saved=new XmlDocument();saved.LoadXml("<settings><plugin path='Plugin.RDCManTheme'><theme mode='Dark'/></plugin></settings>");
    ThemePlugin.PrepareFirstFrame(first,saved.DocumentElement);Assert(first.BackColor==Theme.Background,"Patched first frame dark before Show");
    saved.SelectSingleNode("//theme").Attributes["mode"].Value="Light";
    ThemePlugin.PrepareFirstFrame(first,saved.DocumentElement);Assert(first.BackColor==back,"Patched first frame respects saved Light and restores palette");
    var reader=typeof(ThemePlugin).GetField("WindowsThemeReader",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var actual=reader.GetValue(null);
    saved.SelectSingleNode("//theme").Attributes["mode"].Value="System";reader.SetValue(null,new Func<bool>(()=>true));
    ThemePlugin.PrepareFirstFrame(first,saved.DocumentElement);Assert(first.BackColor==Theme.Background,"Patched first frame respects System dark");
    reader.SetValue(null,new Func<bool>(()=>false));ThemePlugin.PrepareFirstFrame(first,saved.DocumentElement);Assert(first.BackColor==back,"Patched first frame respects System light");reader.SetValue(null,actual);
   }
   using(var catalog=new DirectoryCatalog(root,"Plugin.*.dll"))
   using(var container=new CompositionContainer(catalog))
   using(var form=new HostForm()){
    var exports=container.GetExports<IPlugin>().ToArray();Assert(exports.Length==1,"MEF export count");
    var plugin=(ThemePlugin)exports[0].Value;
    var context=new Context{MainForm=form};
    var tree=new RdcMan.ServerTree{Location=new Point(10,40),Size=new Size(230,530),BackColor=Color.White,ForeColor=Color.Black};tree.Nodes.Add("Serverbaum").Nodes.Add("Testserver");tree.ExpandAll();form.Controls.Add(tree);
    var originalImages=new ImageList{ColorDepth=ColorDepth.Depth32Bit,ImageSize=new Size(16,16)};
    for(int i=0;i<8;i++)originalImages.Images.Add("state"+i,SystemIcons.Application);
    tree.ImageList=originalImages;tree.Nodes[0].ImageIndex=tree.Nodes[0].SelectedImageIndex=5;tree.Nodes[0].Nodes[0].ImageIndex=tree.Nodes[0].Nodes[0].SelectedImageIndex=0;
    var tabs=new TabControl{Location=new Point(255,40),Size=new Size(615,430)};var page=new TabPage("Einstellungen");tabs.TabPages.Add(page);tabs.TabPages.Add("Anzeige");form.Controls.Add(tabs);
    var group=new GroupBox{Text="Optionen",Location=new Point(20,20),Size=new Size(550,210)};page.Controls.Add(group);
    var text=new TextBox{Text="Wiederherstellbarer Testtext",Location=new Point(18,30),Width=320};group.Controls.Add(text);
    var combo=new ComboBox{Location=new Point(18,70),Width=320,DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.AddRange(new object[]{"Auswahl A","Auswahl B"});combo.SelectedIndex=0;group.Controls.Add(combo);
    var button=new Button{Text="Schaltfläche",Location=new Point(370,30),Width=150};group.Controls.Add(button);
    var disabled=new Button{Text="Deaktiviert",Enabled=false,Location=new Point(370,70),Width=150};group.Controls.Add(disabled);
    group.Controls.Add(new CheckBox{Text="Aktiviert",Checked=true,Location=new Point(18,115),AutoSize=true});
    group.Controls.Add(new NumericUpDown{Location=new Point(370,115),Width=150});
    var list=new ListView{View=View.Details,Location=new Point(20,250),Size=new Size(550,130)};list.Columns.Add("Name",250);list.Columns.Add("Status",250);list.Items.Add(new ListViewItem(new[]{"Testeintrag","Bereit"}));page.Controls.Add(list);
    var originalBack=form.BackColor;var originalFore=form.ForeColor;var buttonFlat=button.FlatStyle;var visual=button.UseVisualStyleBackColor;var comboMode=combo.DrawMode;var tabMode=tabs.DrawMode;var listOwner=list.OwnerDraw;var renderer=form.MainMenuStrip.Renderer;var groupFlat=group.FlatStyle;
    plugin.PreLoad(context,null);
    Assert(Theme.DarkEnabled&&form.BackColor==Theme.Background,"Default dark already at PreLoad");
    Assert(text.BackColor==Theme.Panel,"Existing controls dark before PostLoad");
    var startupControl=new TextBox{Text="During file loading",Location=new Point(280,510),Width=200};form.Controls.Add(startupControl);
    var comment=new TextBox{Multiline=true,ScrollBars=ScrollBars.Vertical,Text=string.Join(Environment.NewLine,Enumerable.Range(1,30).Select(i=>"Comment line "+i)),Location=new Point(510,480),Size=new Size(250,90)};form.Controls.Add(comment);
    Assert(startupControl.BackColor==Theme.Panel,"Control added between PreLoad and PostLoad is dark");
    var startupLight=new XmlDocument();startupLight.LoadXml("<plugin><theme mode='Light'/></plugin>");
    plugin.PreLoad(context,startupLight.DocumentElement);Assert(!Theme.DarkEnabled&&form.BackColor==originalBack,"Saved light respected at PreLoad");
    plugin.PreLoad(context,null);plugin.SetMode(ThemeMode.Dark);
    plugin.PostLoad(context);plugin.PostLoad(context);form.Show();Application.DoEvents();
    Assert(Theme.DarkEnabled,"Default dark");Assert(form.BackColor==Theme.Background,"Dark form");Assert(text.BackColor==Theme.Panel,"Dark text");Assert(form.MainMenuStrip.Items.Cast<ToolStripItem>().Count(i=>i.Text=="Darstellung")==1,"Menu installed once");
    tree.SimulateLegacyPalette();Assert(tree.BackColor==Theme.Background,"Original focus palette overridden through events");Assert(tree.ForeColor==(tree.Focused?Theme.Text:Theme.Muted),"Tree focus palette");
    var late=new TextBox{Text="Nachträglich hinzugefügt",Location=new Point(18,155),Width=320};group.Controls.Add(late);Assert(late.BackColor==Theme.Panel,"Late control dark");
    text.BackColor=Color.White;text.ForeColor=Color.Black;page.BackColor=SystemColors.Control;
    Assert(text.BackColor==Theme.Panel && text.ForeColor==Theme.Text && page.BackColor==Theme.Panel,"Dialog palette resets compensated immediately");
    var ctx=new ContextMenuStrip();tree.ContextMenuStrip=ctx;var dynamic=new ToolStripMenuItem("Dynamisches Untermenü");dynamic.DropDownItems.Add("Kind");ctx.Items.Add(dynamic);plugin.OnContextMenu(ctx,null);Assert(dynamic.DropDown.Renderer.GetType().Name=="DarkRenderer","Dynamic submenu dark");
    using(var ax=new FakeAxHost()){ax.BackColor=Color.Blue;ax.ForeColor=Color.Yellow;Theme.Apply(ax);Assert(ax.BackColor==Color.Blue&&ax.ForeColor==Color.Yellow,"AxHost excluded");}
    Image(form,"plugin-dark.png");
    Assert(tree.ImageList!=originalImages && tree.ImageList.Images.Count==8 && tree.ImageList.Images.Keys[5]=="state5","Folder image list copied with stable indexes and keys");
    using(var before=new Bitmap(originalImages.Images[0]))using(var after=new Bitmap(tree.ImageList.Images[0]))Assert(before.GetPixel(8,8)==after.GetPixel(8,8),"Server state image preserved");
    Assert(comment.Multiline && comment.ScrollBars==ScrollBars.Vertical && comment.BackColor==Theme.Panel,"Multiline scrollbar and content retained in dark mode");
    plugin.SetMode(ThemeMode.Light);Application.DoEvents();
    Assert(!Theme.DarkEnabled,"Light flag");Assert(form.BackColor==originalBack&&form.ForeColor==originalFore,"Form restoration");Assert(tree.BackColor==Color.White&&tree.ForeColor==Color.Black,"Tree restoration");Assert(button.FlatStyle==buttonFlat&&button.UseVisualStyleBackColor==visual,"Button style restoration");Assert(combo.DrawMode==comboMode,"Combo mode restoration");Assert(tabs.DrawMode==tabMode,"Tab mode restoration");Assert(list.OwnerDraw==listOwner,"List ownerdraw restoration");Assert(group.FlatStyle==groupFlat,"Group style restoration");Assert(ReferenceEquals(form.MainMenuStrip.Renderer,renderer),"Menu renderer restoration");Assert(text.ForeColor.ToArgb()==originalFore.ToArgb(),"Inherited foreground restored");Assert(late.ForeColor.ToArgb()==originalFore.ToArgb(),"Late inherited foreground restored");
    Image(form,"plugin-light.png");
    Assert(ReferenceEquals(tree.ImageList,originalImages),"Original folder image list restored in Light");
    Assert(comment.ScrollBars==ScrollBars.Vertical && comment.BackColor==SystemColors.Window,"Multiline scrollbar and light palette restored");
    for(int i=0;i<5;i++){plugin.SetMode(ThemeMode.Dark);plugin.SetMode(ThemeMode.Light);}Assert(form.BackColor==originalBack&&combo.DrawMode==comboMode,"Repeated toggle restoration");
    var reader=typeof(ThemePlugin).GetField("WindowsThemeReader",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var actualReader=reader.GetValue(null);
    reader.SetValue(null,new Func<bool>(()=>false));plugin.SetMode(ThemeMode.System);Assert(!Theme.DarkEnabled,"System light selection");
    reader.SetValue(null,new Func<bool>(()=>true));typeof(ThemePlugin).GetMethod("Tick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(plugin,new object[]{null,EventArgs.Empty});Assert(Theme.DarkEnabled,"Live system dark change");
    reader.SetValue(null,new Func<bool>(()=>false));typeof(ThemePlugin).GetMethod("Tick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(plugin,new object[]{null,EventArgs.Empty});Assert(!Theme.DarkEnabled,"Live system light change");reader.SetValue(null,actualReader);
    var doc=new XmlDocument();var wrapper=doc.CreateElement("plugin");wrapper.AppendChild(doc.ImportNode(plugin.SaveSettings(),true));var next=new ThemePlugin();next.PreLoad(context,wrapper);Assert(next.Mode==ThemeMode.System,"Settings roundtrip");next.Shutdown();
    Assert(!plugin.PreFilterMessage(ref message),"Filter does not consume input");
    plugin.SetMode(ThemeMode.Dark);
    using(var dialog=new Form{Text="Testdialog",ShowInTaskbar=false,Location=new Point(-20000,-20000),StartPosition=FormStartPosition.Manual}){
     var field=new TextBox{Text="First paint"};dialog.Controls.Add(field);
     bool activated=false;
     dialog.Activated+=(s,e)=>{activated=true;Assert(dialog.BackColor==Theme.Background && field.BackColor==Theme.Panel,"Dialog dark before activation, before idle/queued paint");};
     dialog.Show();Application.DoEvents();Assert(activated,"Dialog activation hook exercised");Assert(dialog.BackColor==Theme.Background,"New dialog observed");dialog.Close();
    }
    using(var modal=new Form{ShowInTaskbar=false,Location=new Point(-20000,-20000),StartPosition=FormStartPosition.Manual}){
     var field=new TextBox();modal.Controls.Add(field);bool activated=false;
     modal.Load+=(s,e)=>{modal.BackColor=SystemColors.Control;field.BackColor=Color.White;field.ForeColor=Color.Black;};
     modal.Shown+=(s,e)=>{field.BackColor=Color.White;Assert(field.BackColor==Theme.Panel,"Shown callback cannot expose light field");};
     modal.Activated+=(s,e)=>{activated=true;Assert(modal.BackColor==Theme.Background && field.BackColor==Theme.Panel,"ShowDialog dark at activation");modal.BeginInvoke(new Action(()=>modal.Close()));};
     modal.ShowDialog(form);Assert(activated,"Modal dialog activation exercised");
    }
    var rdcDialogType=typeof(IPlugin).Assembly.GetType("RDCMan.Settings.TabbedSettingsDialog",true);
    using(var actual=(Form)Activator.CreateInstance(rdcDialogType,System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance,null,new object[]{"Actual RDCMan dialog","OK",form},null)){
     actual.StartPosition=FormStartPosition.Manual;actual.Location=new Point(-20000,-20000);
     var actualTabs=actual.Controls.OfType<TabControl>().Single();
     for(int i=0;i<8;i++){var p=new TabPage("Real dialog tab "+i);p.Controls.Add(new TextBox{Text="Test"});actualTabs.TabPages.Add(p);}
     var openingButton=new Button{Text="OK",Location=new Point(10,200)};actual.Controls.Add(openingButton);openingButton.BringToFront();actual.ResumeLayout();bool shown=false;
     actual.Shown+=(s,e)=>{shown=true;Assert(actual.BackColor==Theme.Background && actualTabs.TabPages[0].Controls[0].BackColor==Theme.Panel,"Actual RDCMan TabbedSettingsDialog dark after its ShownCallback");Assert(actual.Opacity==0,"Actual RDCMan dialog hidden during Shown construction");actual.BeginInvoke(new Action(()=>actual.BeginInvoke(new Action(()=>{Assert(actual.Opacity==1,"Actual RDCMan dialog revealed after Shown callbacks");Image(actual,"actual-rdcman-dialog.png");actual.Close();}))));};
     actual.ShowDialog(form);Assert(shown,"Actual RDCMan dialog exercised without Main or RDP connections");
    }
    plugin.Shutdown();Assert(form.BackColor==originalBack,"Shutdown restores original colors");Assert(!form.MainMenuStrip.Items.Cast<ToolStripItem>().Any(i=>i.Text=="Darstellung"),"Shutdown removes own menu");
    File.WriteAllText(Path.Combine(root,"plugin-test-result.txt"),"PASS: MEF DirectoryCatalog Plugin.*.dll discovery and IPlugin export; plugin lifecycle; dark/light/system selection; original palette reset compensation; late controls and dialogs; dynamic submenus; AxHost exclusion; original colors, inherited values, renderer, FlatStyle, DrawMode and OwnerDraw restored; repeated toggles; setting XML roundtrip; shutdown. Synthetic host only; RDCMan Main and connection code were not executed.");
   }
  }catch(Exception ex){File.WriteAllText(Path.Combine(root,"plugin-test-result.txt"),"FAIL: "+ex);Environment.ExitCode=1;}
 }
 static Message message=Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero);
 static ref Message refMessage(){return ref message;}
}



