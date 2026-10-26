'use strict';
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto');
const {spawn}=require('node:child_process'),{pipeline}=require('node:stream/promises'),{Transform}=require('node:stream');
const magic=Buffer.from('ORBEBAK1');
function headerFor(publicKey,key,iv){const wrapped=crypto.publicEncrypt({key:publicKey,oaepHash:'sha256'},key),size=Buffer.alloc(2);size.writeUInt16BE(wrapped.length);return Buffer.concat([magic,size,wrapped,iv]);}
async function encrypt(input,output,publicKey,maxBytes=1024*1024*1024){
 const key=crypto.randomBytes(32),iv=crypto.randomBytes(12),header=headerFor(publicKey,key,iv),cipher=crypto.createCipheriv('aes-256-gcm',key,iv);cipher.setAAD(header);
 const partial=output+'.partial-'+crypto.randomUUID();let bytes=0;
 try{fs.writeFileSync(partial,header,{flag:'wx',mode:0o600});const limit=new Transform({transform(chunk,encoding,next){bytes+=chunk.length;next(bytes>maxBytes?new Error('Backup exceeds configured limit'):null,chunk);}});
  await pipeline(input,limit,cipher,fs.createWriteStream(partial,{flags:'a',mode:0o600}));fs.appendFileSync(partial,cipher.getAuthTag());fs.renameSync(partial,output);
 }catch(error){fs.rmSync(partial,{force:true});throw error;}finally{key.fill(0);}
}
async function decrypt(input,output,privateKey){
 if(fs.existsSync(output))throw new Error('Output already exists');
 const fd=fs.openSync(input,'r');let key;const partial=output+'.partial-'+crypto.randomUUID();
 try{const prefix=Buffer.alloc(10);if(fs.readSync(fd,prefix,0,10,0)!==10||!prefix.subarray(0,8).equals(magic))throw new Error('Invalid backup header');
  const size=prefix.readUInt16BE(8),total=fs.fstatSync(fd).size;if(size<256||size>1024||total<10+size+12+16)throw new Error('Invalid backup length');
  const header=Buffer.alloc(10+size+12);fs.readSync(fd,header,0,header.length,0);const tag=Buffer.alloc(16);fs.readSync(fd,tag,0,16,total-16);
  key=crypto.privateDecrypt({key:privateKey,oaepHash:'sha256'},header.subarray(10,10+size));const cipher=crypto.createDecipheriv('aes-256-gcm',key,header.subarray(10+size));cipher.setAAD(header);cipher.setAuthTag(tag);
  await pipeline(fs.createReadStream(input,{start:header.length,end:total-17}),cipher,fs.createWriteStream(partial,{flags:'wx',mode:0o600}));fs.renameSync(partial,output);
 }catch(error){fs.rmSync(partial,{force:true});throw error;}finally{fs.closeSync(fd);key?.fill(0);}
}
function prune(directory,days=7,now=new Date()){
 const cutoff=Date.UTC(now.getUTCFullYear(),now.getUTCMonth(),now.getUTCDate())-(days-1)*86400000;let removed=0;
 for(const entry of fs.readdirSync(directory,{withFileTypes:true})){
  if(!entry.isFile())continue;
  const match=/^orbe-(\d{4}-\d{2}-\d{2})-[a-f0-9-]+\.orbebak$/.exec(entry.name),file=path.join(directory,entry.name);
  const stale=/^orbe-[\d-]+-[a-f0-9-]+\.orbebak(?:\.candidate)?(?:\.partial-[a-f0-9-]+)?$/.test(entry.name)&&!match&&fs.statSync(file).mtimeMs<now.getTime()-86400000;
  if(match&&Date.parse(match[1]+'T00:00:00Z')<cutoff||stale){fs.unlinkSync(file);removed++;}
 }
 return removed;
}
function start(config,root=__dirname){
 if(!config?.Enabled)return()=>{};
 const directory=path.resolve(root,config.Directory||'.orbe-backups'),tools=path.join(root,'pg-tools'),binary=path.join(tools,'pg_dump');
 if(directory.startsWith(path.join(root,'server','wwwroot')))throw new Error('Backup directory cannot be public');
 fs.mkdirSync(directory,{recursive:true,mode:0o700});fs.chmodSync(binary,0o755);
 let running=false,nextAttempt=0,controller;const ready=()=>fs.readdirSync(directory,{withFileTypes:true}).filter(x=>x.isFile()&&/^orbe-\d{4}-\d{2}-\d{2}-[a-f0-9-]+\.orbebak$/.test(x.name)).map(x=>x.name);
 async function tick(){const now=new Date(),day=now.toISOString().slice(0,10);if(running||Date.now()<nextAttempt||now.getUTCHours()<(config.HourUtc??6)||ready().some(x=>x.startsWith('orbe-'+day+'-')))return;
  running=true;controller=new AbortController();const timer=setTimeout(()=>controller.abort(),300000);timer.unref();let child;
  const output=path.join(directory,'orbe-'+day+'-'+crypto.randomUUID()+'.orbebak');
  try{child=spawn(binary,['--format=custom','--no-owner','--no-acl','--no-password'],{cwd:root,env:{...process.env,...config.Environment,LD_LIBRARY_PATH:tools+(process.env.LD_LIBRARY_PATH?':'+process.env.LD_LIBRARY_PATH:''),PGAPPNAME:'orbe-daily-backup',PGCONNECT_TIMEOUT:'15'},stdio:['ignore','pipe','ignore'],signal:controller.signal});
   const exit=new Promise((resolve,reject)=>{child.once('error',reject);child.once('close',code=>code===0?resolve():reject(new Error('Database backup process failed')));});exit.catch(()=>{});
   await encrypt(child.stdout,output+'.candidate',config.PublicKeyPem,config.MaxBytes);await exit;fs.renameSync(output+'.candidate',output);
   prune(directory,config.RetentionDays??7);
   console.log('Orbe daily backup completed (encrypted).');
  }catch{if(child&&!child.killed)child.kill('SIGTERM');fs.rmSync(output,{force:true});fs.rmSync(output+'.candidate',{force:true});nextAttempt=Date.now()+3600000;console.error('Orbe daily backup failed; retry in one hour.');}
  finally{clearTimeout(timer);running=false;}
 }
 const interval=setInterval(()=>{tick().catch(()=>console.error('Orbe backup scheduler failed.'));},60000);interval.unref();tick().catch(()=>console.error('Orbe backup scheduler failed.'));
 return()=>{clearInterval(interval);controller?.abort();};
}
module.exports={encrypt,decrypt,start,prune};
if(require.main===module){let data='';process.stdin.setEncoding('utf8');process.stdin.on('data',chunk=>{data+=chunk;if(data.length>65536)process.exit(1);});process.stdin.on('end',async()=>{try{const job=JSON.parse(data);await decrypt(job.input,job.output,job.privateKey);console.log('Backup authenticated and decrypted.');}catch{console.error('Backup decryption failed. No restored output was retained.');process.exitCode=1;}});}
