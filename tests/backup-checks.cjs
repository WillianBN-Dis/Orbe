'use strict';
const assert=require('node:assert/strict'),fs=require('node:fs'),os=require('node:os'),path=require('node:path'),crypto=require('node:crypto'),{Readable}=require('node:stream');
const {encrypt,decrypt}=require('../deploy/discloud/backup');
(async()=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'orbe-backup-check-')),keys=crypto.generateKeyPairSync('rsa',{modulusLength:2048,publicKeyEncoding:{type:'spki',format:'pem'},privateKeyEncoding:{type:'pkcs8',format:'pem'}});
 const input=crypto.randomBytes(1024*1024+71),file=path.join(root,'snapshot.orbebak'),plain=path.join(root,'restored.dump');let checks=0;
 try{
  await encrypt(Readable.from([input.subarray(0,17),input.subarray(17)]),file,keys.publicKey);await decrypt(file,plain,keys.privateKey);assert.deepEqual(fs.readFileSync(plain),input);console.log('PASS streaming encryption/decryption preserves dump');checks++;
  for(const position of [0,12,280,fs.statSync(file).size-1]){const data=fs.readFileSync(file);data[position]^=1;const corrupt=path.join(root,'corrupt-'+position);fs.writeFileSync(corrupt,data);const output=corrupt+'.dump';await assert.rejects(decrypt(corrupt,output,keys.privateKey));assert.equal(fs.existsSync(output),false);assert.equal(fs.readdirSync(root).some(x=>x.startsWith(path.basename(output)+'.partial-')),false);checks++;}
  console.log('PASS header, wrapped key, ciphertext and authentication tag tampering rejected');
  const truncated=path.join(root,'truncated');fs.writeFileSync(truncated,fs.readFileSync(file).subarray(0,25));await assert.rejects(decrypt(truncated,truncated+'.dump',keys.privateKey));checks++;
  const limited=path.join(root,'oversize');await assert.rejects(encrypt(Readable.from([input]),limited,keys.publicKey,1024));assert.equal(fs.existsSync(limited),false);checks++;
  await assert.rejects(decrypt(file,plain,keys.privateKey));assert.deepEqual(fs.readFileSync(plain),input);checks++;
  console.log(checks+' backup checks passed with synthetic data only.');
 }finally{fs.rmSync(root,{recursive:true});}
})().catch(error=>{console.error(error.message);process.exitCode=1;});
