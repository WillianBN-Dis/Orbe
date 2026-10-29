// Compatibility entrypoint for installed Orbe apps using the previous address.
// No database access, credentials, logs of requests or application workers here.
const http=require('node:http'),https=require('node:https');
const target=new URL('https://orbe.discloud.dev');
const oldOrigin='https://orbe-willianbn.discloud.dev';
const hop=new Set(['host','connection','keep-alive','proxy-authenticate','proxy-authorization','te','trailer','transfer-encoding','upgrade','forwarded','x-forwarded-for','x-forwarded-host','x-forwarded-proto']);
function createGateway(send=https.request){return http.createServer((req,res)=>{
 const path=req.url||'/';
 if(!path.startsWith('/')||path.startsWith('//')){res.writeHead(400);res.end();return;}

 const headers={};for(const [key,value] of Object.entries(req.headers))if(!hop.has(key))headers[key]=value;
 if(path.split('?')[0]==='/api/v1/public/connection')headers['accept-encoding']='identity';
 // Both origins belong to this application; cookie/CSRF sessions remain usable.
 if(headers.origin===oldOrigin)headers.origin=target.origin;
 if(headers.referer?.startsWith(oldOrigin+'/'))headers.referer=target.origin+headers.referer.substring(oldOrigin.length);
 const upstream=send({protocol:'https:',hostname:target.hostname,port:443,path,method:req.method,headers,timeout:30000},response=>{
  const output={};for(const [key,value] of Object.entries(response.headers))if(!hop.has(key))output[key]=value;
  if(path.split('?')[0]==='/api/v1/public/connection'&&response.statusCode===200){
   let body='';response.setEncoding('utf8');response.on('data',chunk=>{body+=chunk;if(body.length>16384)response.destroy();});
   response.on('end',()=>{try{const data=JSON.parse(body);if(data.apiUrl===target.origin)data.apiUrl=oldOrigin;res.writeHead(200,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(JSON.stringify(data));}catch{res.writeHead(502);res.end();}});
   response.on('error',()=>{if(!res.headersSent)res.writeHead(502);res.end();});return;
  }
  res.writeHead(response.statusCode||502,output);response.pipe(res);
 });
 upstream.on('timeout',()=>upstream.destroy());
 upstream.on('error',()=>{if(!res.headersSent)res.writeHead(502,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end('{"message":"O Orbe está temporariamente indisponível. Tente novamente."}');});
 req.on('aborted',()=>upstream.destroy());res.on('close',()=>{if(!res.writableEnded)upstream.destroy();});req.pipe(upstream);
 });}
module.exports={createGateway};
if(require.main===module){const server=createGateway();server.listen(8080,'0.0.0.0');for(const signal of ['SIGTERM','SIGINT'])process.on(signal,()=>server.close(()=>process.exit(0)));}
