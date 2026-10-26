// Discloud's supported Node entrypoint starts the actual self-contained ASP.NET Core server.
const fs=require('node:fs'),path=require('node:path'),{spawn}=require('node:child_process');
const root=path.join(__dirname,'server'),binary=path.join(root,'Orbe.Server');
fs.chmodSync(binary,0o755);
const config=JSON.parse(fs.readFileSync(path.join(root,'orbe.secrets.json'),'utf8').replace(/^\uFEFF/,''));
const stopBackups=require('./backup').start(config.Backups,__dirname);
const child=spawn(binary,[],{cwd:root,stdio:'inherit',env:{...process.env,ASPNETCORE_ENVIRONMENT:'Production',ASPNETCORE_URLS:'http://0.0.0.0:8080',DOTNET_GCHeapHardLimit:process.env.DOTNET_GCHeapHardLimit||'0x10000000',DOTNET_EnableDiagnostics:'0'}});
for(const signal of ['SIGTERM','SIGINT'])process.on(signal,()=>{stopBackups();child.kill(signal);});
child.on('error',()=>{console.error('Não foi possível iniciar o servidor Orbe. Confira o pacote Linux.');process.exit(1);});
child.on('exit',code=>{stopBackups();process.exit(code??1);});
