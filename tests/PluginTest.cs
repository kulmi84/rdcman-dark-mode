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
   using(var catalog=new DirectoryCatalog(root,"Plugin.*.dll"))
   using(var container=new CompositionContainer(catalog))
   using(var form=new HostForm()){
    var exports=container.GetExports<IPlugin>().ToArray();Assert(exports.Length==1,"MEF export count");
    var plugin=(ThemePlugin)exports[0].Value;
    var context=new Context{MainForm=form};
    var tree=new RdcMan.ServerTree{Location=new Point(10,40),Size=new Size(230,530),BackColor=Color.White,ForeColor=Color.Black};tree.Nodes.Add("Serverbaum").Nodes.Add("Testserver");tree.ExpandAll();form.Controls.Add(tree);
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
    plugin.PreLoad(context,null);plugin.PostLoad(context);form.Show();Application.DoEvents();
    Assert(Theme.DarkEnabled,"Default dark");Assert(form.BackColor==Theme.Background,"Dark form");Assert(text.BackColor==Theme.Panel,"Dark text");Assert(form.MainMenuStrip.Items.Cast<ToolStripItem>().Count(i=>i.Text=="Darstellung")==1,"Menu installed once");
    tree.SimulateLegacyPalette();Assert(tree.BackColor==Theme.Background,"Original focus palette overridden through events");Assert(tree.ForeColor==(tree.Focused?Theme.Text:Theme.Muted),"Tree focus palette");
    var late=new TextBox{Text="Nachträglich hinzugefügt",Location=new Point(18,155),Width=320};group.Controls.Add(late);Assert(late.BackColor==Theme.Panel,"Late control dark");
    var ctx=new ContextMenuStrip();tree.ContextMenuStrip=ctx;var dynamic=new ToolStripMenuItem("Dynamisches Untermenü");dynamic.DropDownItems.Add("Kind");ctx.Items.Add(dynamic);plugin.OnContextMenu(ctx,null);Assert(dynamic.DropDown.Renderer.GetType().Name=="DarkRenderer","Dynamic submenu dark");
    using(var ax=new FakeAxHost()){ax.BackColor=Color.Blue;ax.ForeColor=Color.Yellow;Theme.Apply(ax);Assert(ax.BackColor==Color.Blue&&ax.ForeColor==Color.Yellow,"AxHost excluded");}
    Image(form,"plugin-dark.png");
    plugin.SetMode(ThemeMode.Light);Application.DoEvents();
    Assert(!Theme.DarkEnabled,"Light flag");Assert(form.BackColor==originalBack&&form.ForeColor==originalFore,"Form restoration");Assert(tree.BackColor==Color.White&&tree.ForeColor==Color.Black,"Tree restoration");Assert(button.FlatStyle==buttonFlat&&button.UseVisualStyleBackColor==visual,"Button style restoration");Assert(combo.DrawMode==comboMode,"Combo mode restoration");Assert(tabs.DrawMode==tabMode,"Tab mode restoration");Assert(list.OwnerDraw==listOwner,"List ownerdraw restoration");Assert(group.FlatStyle==groupFlat,"Group style restoration");Assert(ReferenceEquals(form.MainMenuStrip.Renderer,renderer),"Menu renderer restoration");Assert(text.ForeColor.ToArgb()==originalFore.ToArgb(),"Inherited foreground restored");Assert(late.ForeColor.ToArgb()==originalFore.ToArgb(),"Late inherited foreground restored");
    Image(form,"plugin-light.png");
    for(int i=0;i<5;i++){plugin.SetMode(ThemeMode.Dark);plugin.SetMode(ThemeMode.Light);}Assert(form.BackColor==originalBack&&combo.DrawMode==comboMode,"Repeated toggle restoration");
    var reader=typeof(ThemePlugin).GetField("WindowsThemeReader",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var actualReader=reader.GetValue(null);
    reader.SetValue(null,new Func<bool>(()=>false));plugin.SetMode(ThemeMode.System);Assert(!Theme.DarkEnabled,"System light selection");
    reader.SetValue(null,new Func<bool>(()=>true));typeof(ThemePlugin).GetMethod("Tick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(plugin,new object[]{null,EventArgs.Empty});Assert(Theme.DarkEnabled,"Live system dark change");
    reader.SetValue(null,new Func<bool>(()=>false));typeof(ThemePlugin).GetMethod("Tick",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(plugin,new object[]{null,EventArgs.Empty});Assert(!Theme.DarkEnabled,"Live system light change");reader.SetValue(null,actualReader);
    var doc=new XmlDocument();var wrapper=doc.CreateElement("plugin");wrapper.AppendChild(doc.ImportNode(plugin.SaveSettings(),true));var next=new ThemePlugin();next.PreLoad(context,wrapper);Assert(next.Mode==ThemeMode.System,"Settings roundtrip");
    Assert(!plugin.PreFilterMessage(ref message),"Filter does not consume input");
    plugin.SetMode(ThemeMode.Dark);
    using(var dialog=new Form{Text="Testdialog",ShowInTaskbar=false,Location=new Point(-20000,-20000),StartPosition=FormStartPosition.Manual}){dialog.Controls.Add(new Label{Text="Dialogtext",AutoSize=true});dialog.Show();Application.DoEvents();Assert(dialog.BackColor==Theme.Background,"New dialog observed");dialog.Close();}
    plugin.Shutdown();Assert(form.BackColor==originalBack,"Shutdown restores original colors");Assert(!form.MainMenuStrip.Items.Cast<ToolStripItem>().Any(i=>i.Text=="Darstellung"),"Shutdown removes own menu");
    File.WriteAllText(Path.Combine(root,"plugin-test-result.txt"),"PASS: MEF DirectoryCatalog Plugin.*.dll discovery and IPlugin export; plugin lifecycle; dark/light/system selection; original palette reset compensation; late controls and dialogs; dynamic submenus; AxHost exclusion; original colors, inherited values, renderer, FlatStyle, DrawMode and OwnerDraw restored; repeated toggles; setting XML roundtrip; shutdown. Synthetic host only; RDCMan Main and connection code were not executed.");
   }
  }catch(Exception ex){File.WriteAllText(Path.Combine(root,"plugin-test-result.txt"),"FAIL: "+ex);Environment.ExitCode=1;}
 }
 static Message message=Message.Create(IntPtr.Zero,0,IntPtr.Zero,IntPtr.Zero);
 static ref Message refMessage(){return ref message;}
}
