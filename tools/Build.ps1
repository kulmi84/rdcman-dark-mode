param(
 [Parameter(Mandatory=$true)][string]$RdcManExe,
 [switch]$Test
)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSVersion.Major -lt 7){throw 'PowerShell 7 is required.'}
$projectRoot=Split-Path -Parent $PSScriptRoot
$buildDir=Join-Path $projectRoot 'build'
$refsDir=Join-Path $buildDir 'refs'
New-Item -ItemType Directory -Path $refsDir -Force | Out-Null
$sourcePath=(Resolve-Path -LiteralPath $RdcManExe).Path
$code=@'
using System;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
public static class RdcThemeBuild {
 public static int Extract(string path,string dest) {
  byte[] bytes=File.ReadAllBytes(path);
  byte[] sig=Convert.FromHexString("8b1202b96a612038727b930214d7a03213f5b9e6efae3318ee3b2dce24b36aae");
  int index=-1;
  for(int i=8;i<=bytes.Length-sig.Length;i++){if(bytes[i]!=sig[0])continue;bool match=true;for(int j=1;j<sig.Length;j++)if(bytes[i+j]!=sig[j]){match=false;break;}if(match){index=i;break;}}
  if(index<0)throw new InvalidDataException("No .NET single-file bundle found.");
  long header=BitConverter.ToInt64(bytes,index-8);
  using(var stream=new MemoryStream(bytes))using(var reader=new BinaryReader(stream)) {
   stream.Position=header;uint major=reader.ReadUInt32();uint minor=reader.ReadUInt32();int count=reader.ReadInt32();
   if(major!=6 || minor!=0 || count<1 || count>10000)throw new InvalidDataException("Unsupported bundle format.");
   reader.ReadString();stream.Position+=40;int extracted=0;
   for(int i=0;i<count;i++) {
    long offset=reader.ReadInt64(),size=reader.ReadInt64(),compressed=reader.ReadInt64();byte type=reader.ReadByte();string name=reader.ReadString();
    if(type!=1 || name.Contains("/") || name.Contains("\\") || !name.EndsWith(".dll",StringComparison.OrdinalIgnoreCase))continue;
    if(offset<0 || size<0 || compressed<0 || offset+(compressed==0?size:compressed)>bytes.LongLength)throw new InvalidDataException("Invalid payload range.");
    byte[] payload=new byte[checked((int)size)];
    if(compressed==0)Buffer.BlockCopy(bytes,checked((int)offset),payload,0,payload.Length);
    else using(var input=new MemoryStream(bytes,checked((int)offset),checked((int)compressed)))using(var inflate=new DeflateStream(input,CompressionMode.Decompress)){
     int pos=0,n;while(pos<payload.Length && (n=inflate.Read(payload,pos,payload.Length-pos))>0)pos+=n;if(pos!=payload.Length)throw new InvalidDataException("Decompression size mismatch.");
    }
    File.WriteAllBytes(Path.Combine(dest,name),payload);extracted++;
   }
   if(!File.Exists(Path.Combine(dest,"RDCMan.dll")))throw new InvalidDataException("RDCMan.dll not found.");
   return extracted;
  }
 }
 public static void Compile(string root,bool test) {
  string build=Path.Combine(root,"build");
  var refs=Directory.GetFiles(Path.Combine(build,"refs"),"*.dll").Select(path=>MetadataReference.CreateFromFile(path)).ToList();
  string[] sources=test?new[]{Path.Combine(root,"tests","PluginTest.cs")}:new[]{Path.Combine(root,"src","Theme.cs"),Path.Combine(root,"src","Plugin.cs")};
  if(test)refs.Add(MetadataReference.CreateFromFile(Path.Combine(build,"Plugin.RDCManTheme.dll")));
  var compilation=CSharpCompilation.Create(test?"ThemePluginTest":"Plugin.RDCManTheme",sources.Select(file=>CSharpSyntaxTree.ParseText(File.ReadAllText(file))),refs,new CSharpCompilationOptions(test?OutputKind.WindowsApplication:OutputKind.DynamicallyLinkedLibrary,optimizationLevel:OptimizationLevel.Release));
  var result=compilation.Emit(Path.Combine(build,test?"ThemePluginTest.dll":"Plugin.RDCManTheme.dll"));
  foreach(var d in result.Diagnostics)if(d.Severity==DiagnosticSeverity.Error || d.Severity==DiagnosticSeverity.Warning)Console.WriteLine(d.ToString());
  if(!result.Success)throw new Exception("Compilation failed.");
 }
}
'@
Add-Type -TypeDefinition $code -CompilerOptions '/nowarn:1701' -IgnoreWarnings -ReferencedAssemblies @('Microsoft.CodeAnalysis','Microsoft.CodeAnalysis.CSharp','System.Collections.Immutable','System.Runtime','System.Collections','System.Linq','System.Console','System.IO.Compression')
# Keep each build's references separate to avoid mixing versions across runs.
Get-ChildItem -LiteralPath $refsDir -Filter '*.dll' -File | Remove-Item
$count=[RdcThemeBuild]::Extract($sourcePath,$refsDir)
Write-Output "Extracted $count managed references; source EXE unchanged."
[RdcThemeBuild]::Compile($projectRoot,$false)
Get-FileHash -LiteralPath (Join-Path $buildDir 'Plugin.RDCManTheme.dll')
if($Test){
 [RdcThemeBuild]::Compile($projectRoot,$true)
 foreach($name in @('RDCMan.dll','System.ComponentModel.Composition.dll','AxMSTSCLib.dll','MSTSCLib.dll','WinRT.Runtime.dll','Microsoft.Windows.SDK.NET.dll')){
  $refPath=Join-Path $refsDir $name
  if(Test-Path -LiteralPath $refPath){Copy-Item -LiteralPath $refPath -Destination $buildDir -Force}
 }
 $major=([Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $refsDir 'System.Private.CoreLib.dll'))).FileMajorPart
 if($major -notin @(8,10)){throw "Unsupported test runtime major: $major"}
 @{runtimeOptions=@{tfm="net$major.0";framework=@{name='Microsoft.WindowsDesktop.App';version="$major.0.0"}}} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $buildDir 'ThemePluginTest.runtimeconfig.json')
 $dotnet=(Get-Command dotnet -ErrorAction Stop).Source
 $testDll=Join-Path $buildDir 'ThemePluginTest.dll'
 $process=Start-Process -FilePath $dotnet -ArgumentList ('"'+$testDll+'"') -WindowStyle Hidden -PassThru -RedirectStandardError (Join-Path $buildDir 'test-stderr.txt')
 if(!$process.WaitForExit(30000)){Stop-Process -Id $process.Id;throw 'Synthetic test timed out.'}
 $resultPath=Join-Path $buildDir 'plugin-test-result.txt'
 if($process.ExitCode -ne 0){if(Test-Path -LiteralPath $resultPath){Get-Content -LiteralPath $resultPath};throw "Synthetic test failed: $($process.ExitCode)"}
 Get-Content -LiteralPath $resultPath
}
