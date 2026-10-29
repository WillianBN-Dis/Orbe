const assert=require('node:assert/strict'),{PassThrough}=require('node:stream');
const {createGateway}=require('../deploy/discloud/legacy');
(async()=>{
 let last;
 const server=createGateway((options,callback)=>{last=options;const request=new PassThrough();let body='';request.on('data',c=>body+=c);request.on('end',()=>{const reply=new PassThrough();reply.statusCode=200;reply.headers={'content-type':'application/json','connection':'close'};callback(reply);reply.end(JSON.stringify({apiUrl:'https://orbe.discloud.dev',body}));});return request;});
 await new Promise(r=>server.listen(0,'127.0.0.1',r));const root='http://127.0.0.1:'+server.address().port;
 try{
  let r=await fetch(root+'/?x=1',{redirect:'manual'});assert.equal(r.status,307);assert.equal(r.headers.get('location'),'https://orbe.discloud.dev/?x=1');
  r=await fetch(root+'/api/v1/public/connection');assert.equal((await r.json()).apiUrl,'https://orbe.discloud.dev');assert.equal(last.hostname,'orbe.discloud.dev');
  r=await fetch(root+'/api/v1/bookings',{method:'POST',headers:{Authorization:'Bearer synthetic-qa',Origin:'https://orbe-willianbn.discloud.dev','Content-Type':'application/json','X-Forwarded-For':'192.0.2.1'},body:'{"qa":true}'});assert.equal((await r.json()).body,'{"qa":true}');assert.equal(last.headers.authorization,'Bearer synthetic-qa');assert.equal(last.headers.origin,'https://orbe.discloud.dev');assert.equal(last.headers['x-forwarded-for'],undefined);
  assert.equal(last.headers.host,undefined);assert.equal(last.method,'POST');
  console.log('PASS gateway: redirect, descoberta200, destino fixoHTTPS, POST/corpo/token preservados, origem confiável e descarte de headers encaminhados.');
 }finally{await new Promise(r=>server.close(r));}
})().catch(e=>{console.error(e);process.exitCode=1;});
