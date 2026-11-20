'use strict';
const main=document.querySelector('#main'),nav=document.querySelector('#nav'),dialog=document.querySelector('#dialog'),dialogContent=document.querySelector('#dialog-content');
dialog.addEventListener('close',()=>dialogContent.replaceChildren());
const navToggle=document.querySelector('#nav-toggle'),siteHeader=document.querySelector('header');
function closeNavigation(){siteHeader.classList.remove('nav-open');navToggle.setAttribute('aria-expanded','false');}
navToggle.addEventListener('click',()=>{const open=navToggle.getAttribute('aria-expanded')!=='true';siteHeader.classList.toggle('nav-open',open);navToggle.setAttribute('aria-expanded',String(open));});
nav.addEventListener('click',e=>{if(e.target.closest('button'))closeNavigation();});
document.addEventListener('click',e=>{if(!siteHeader.contains(e.target))closeNavigation();});
document.addEventListener('keydown',e=>{if(e.key==='Escape'&&navToggle.getAttribute('aria-expanded')==='true'){closeNavigation();navToggle.focus();}});
window.matchMedia('(min-width:851px)').addEventListener('change',closeNavigation);
let business="",bookingFilter="";
let me=null,info={name:'Orbe',timeZoneId:'America/Sao_Paulo',cancellationHours:24},csrf='',noticeTimer;
const state={service:null,employee:null,day:null,slot:null,key:null,page:0};
function el(tag,props={},...children){const n=document.createElement(tag);for(const[k,v]of Object.entries(props)){if(k==='class')n.className=v;else if(k.startsWith('on'))n[k]=v;else if(k.includes('-'))n.setAttribute(k,String(v));else if(v!==null&&v!==undefined)n[k]=v;}for(const child of children.flat(Infinity)){if(child!==null&&child!==undefined)n.append(child instanceof Node?child:document.createTextNode(String(child)));}return n;}
const stack=(...c)=>el('div',{class:'stack'},...c),row=(...c)=>el('div',{class:'row'},...c),p=(text,cls='muted')=>el('p',{class:cls},text),heading=(t)=>el('h2',{},t);
function toast(message,error=false){const n=document.querySelector('#notice');n.textContent=message;n.className=error?'error':'';n.hidden=false;clearTimeout(noticeTimer);noticeTimer=setTimeout(()=>n.hidden=true,9000);}
function button(text,action,cls=''){const n=el('button',{type:'button',class:cls},text);n.onclick=async()=>{n.disabled=true;try{await action();}catch(e){toast(e.message,true);}finally{n.disabled=false;}};return n;}
function field(label,type='text',value='',required=true){const input=el('input',{type,value:value??'',required,maxLength:256});return {input,root:el('label',{class:'field'},el('span',{},label),input)};}
function select(label,values,value=''){const input=el('select',{'aria-label':label},...values.map(x=>el('option',{value:x.id},x.name)));input.value=value;return {input,root:el('label',{class:'field'},el('span',{},label),input)};}
function form(fields,submitText,action){const f=el('form',{class:'form'},...fields);const b=el('button',{type:'submit',class:'primary'},submitText);f.append(b);f.onsubmit=async e=>{e.preventDefault();if(!f.reportValidity())return;b.disabled=true;try{await action();}catch(error){toast(error.message,true);}finally{b.disabled=false;}};return f;}
const bookingApi=(item,path,options={})=>api(path,{...options,scope:item.business});
async function api(path,{method='GET',body,key,scope=business}={}){if(method!=='GET'&&!csrf){const r=await fetch('/api/v1/auth/csrf',{credentials:'same-origin'});if(!r.ok)throw Error('O serviço está sendo preparado. Tente novamente em breve.');csrf=(await r.json()).token;}const r=await fetch('/api/v1'+path,{method,credentials:'same-origin',headers:{...(scope?{'X-Orbe-Business':scope}:{}),...(body?{'Content-Type':'application/json'}:{}),...(method!=='GET'?{'X-CSRF-TOKEN':csrf}:{}),...(key?{'Idempotency-Key':key}:{})},body:body?JSON.stringify(body):undefined});const data=r.status===204?null:await r.json().catch(()=>null);if(!r.ok){if(r.status===401){me=null;renderNav();}const error=Error(data?.message??(r.status===429?'Muitas tentativas. Aguarde um pouco.':r.status===403?'Sua conta não tem acesso a esta área.':'Não foi possível concluir. Atualize antes de tentar novamente.'));error.status=r.status;throw error;}return data;}
const money=n=>new Intl.NumberFormat('pt-BR',{style:'currency',currency:'BRL'}).format(Number(n)/100);
function cents(value){
 let text=String(value).trim();
 if(text.includes(',')){if(!/^(\d+|\d{1,3}(\.\d{3})*),\d{1,2}$/.test(text))throw Error('Informe um valor como 120,50.');text=text.replaceAll('.','').replace(',','.');}
 if(!/^\d+(\.\d{1,2})?$/.test(text))throw Error('Informe um valor positivo com até duas casas decimais.');
 const parts=text.split('.'),amount=Number(parts[0])*100+Number((parts[1]??'').padEnd(2,'0'));
 if(!Number.isSafeInteger(amount)||amount<=0)throw Error('Informe um valor positivo válido.');return amount;
}
const servicePrice=(cents,from)=> (from?'A partir de ':'')+money(cents);
const stamp=(t,zone=info.timeZoneId)=>new Intl.DateTimeFormat('pt-BR',{timeZone:zone,dateStyle:'short',timeStyle:'short'}).format(new Date(t));
const time=(t,zone=info.timeZoneId)=>new Intl.DateTimeFormat('pt-BR',{timeZone:zone,hour:'2-digit',minute:'2-digit'}).format(new Date(t));
const pendingEmail=()=>me?.emailConfirmed===false&&!info.testMode;
const isTeam=()=>me?.roles.some(x=>x==='Owner'||x==='Staff'),isOwner=()=>me?.roles.includes('Owner');
function today(){const a=new Intl.DateTimeFormat('en-CA',{timeZone:info.timeZoneId,year:'numeric',month:'2-digit',day:'2-digit'}).formatToParts(new Date());return ['year','month','day'].map(k=>a.find(x=>x.type===k).value).join('-');}
function plus(date,days){const d=new Date(date+'T12:00:00Z');d.setUTCDate(d.getUTCDate()+days);return d.toISOString().slice(0,10);}
function setView(node){noticeViewEpoch++;noticesVisible=false;delete main.dataset.live;delete main.dataset.signature;main.replaceChildren(...(info.testMode?[el('aside',{class:'card test-banner',role:'status'},'Ambiente de testes · Cadastros e reservas liberados sem confirmação de e-mail. Recuperação de senha por e-mail indisponível.')]:[]),node);window.scrollTo({top:0,behavior:'instant'});}
async function allPages(path,size=200){const rows=[];for(let offset=0;;offset+=size){const page=await api(path+(path.includes('?')?'&':'?')+'offset='+offset);rows.push(...page);if(page.length<size)return rows;}}
let noticeViewEpoch=0;let noticeState={items:[],unreadCount:0,total:0},noticeLimit=50,noticeLoading=false,noticesVisible=false;
function noticeNav(){const b=button('Notificações',()=>notifications(),'quiet');b.dataset.notifications='true';if(noticeState.unreadCount)b.append(el('span',{class:'notice-count','aria-label':noticeState.unreadCount+' não lidas'},noticeState.unreadCount>99?'99+':String(noticeState.unreadCount)));return b;}
function updateNoticeBadge(){const old=nav.querySelector('[data-notifications]');if(old)old.replaceWith(noticeNav());}
function renderNotices(){const section=el('section',{class:'stack notifications'},p('SEU CENTRO DE AVISOS','eyebrow'),el('div',{class:'between'},el('div',{},el('h1',{},'Notificações'),p(noticeState.unreadCount?`${noticeState.unreadCount} não lida(s)`:'Seus agendamentos e lembretes, em um só lugar.')),button('Atualizar avisos',()=>notifications(),'quiet')));if(!noticeState.items.length)section.append(el('div',{class:'card notification-empty'},el('span',{class:'notification-bell','aria-hidden':'true'},''),heading('Tudo em dia por aqui'),p('Novos agendamentos e lembretes de 1 dia e 1 hora antes aparecerão aqui.')));for(const n of noticeState.items){const title=n.kind==='Payment'?'Informe o valor recebido':n.kind==='Day'?'Lembrete · 1 dia antes':n.kind==='Hour'?'Lembrete · 1 hora antes':n.kind==='Review'?'Agendamento fixo precisa ser remarcado':n.kind==='Cancellation'?'Agendamento cancelado':'Agendamento confirmado';const card=el('article',{class:'card stack notice-card'+(n.readAt?'':' notice-unread')},el('div',{class:'between'},heading(title),el('span',{class:'pill'},n.readAt?'LIDA':'NOVA')),el('h3',{},n.subject));if(n.person)card.append(p(n.person));card.append(p('Evento: '+stamp(n.occurrenceStart)),p('Aviso de '+stamp(n.deliverAt),'caption'));const actions=row();if(n.kind==='Payment')actions.append(button('Informar valor recebido',async()=>{const scope=n.business??business;const item=await api('/team/appointments/'+n.appointmentId,{scope});await moneyForm(item,scope);},'primary'));const phone=whatsAppNumber(n.clientPhone);if(phone)actions.append(el('a',{href:'https://wa.me/'+phone,target:'_blank',rel:'noopener noreferrer',class:'support-link'},'WhatsApp da cliente'));if(!n.readAt)actions.append(button('Marcar como lida',async()=>{await api('/notifications/'+n.id+'/read',{method:'PUT',body:{}});await notifications();},'quiet'));actions.append(button('Excluir',async()=>{await api('/notifications/'+n.id,{method:'DELETE'});await notifications();},'danger'));card.append(actions);section.append(card);}if(noticeState.items.length<noticeState.total)section.append(button('Carregar mais avisos',async()=>{noticeLimit+=50;await notifications();},'quiet'));const y=window.scrollY;setView(section);noticesVisible=true;window.scrollTo({top:y,behavior:'instant'});}
async function fetchNotices(){const result=await api('/notifications?limit='+Math.min(100,noticeLimit));for(let offset=result.items.length;offset<Math.min(noticeLimit,result.total);offset+=100){const next=await api('/notifications?offset='+offset+'&limit=100');result.items.push(...next.items.slice(0,noticeLimit-result.items.length));if(!next.items.length)break;}return result;}
async function notifications(){if(!me)return login(notifications);const user=me.id,epoch=noticeViewEpoch;const result=await fetchNotices();if(me?.id!==user||epoch!==noticeViewEpoch)return;noticeState=result;updateNoticeBadge();renderNotices();}
async function pollNotices(){if(noticeLoading||!me||pendingEmail()||document.visibilityState!=='visible'||dialog.open)return;noticeLoading=true;const user=me.id;try{const result=await fetchNotices();if(me?.id!==user)return;const changed=JSON.stringify(result)!==JSON.stringify(noticeState);noticeState=result;updateNoticeBadge();if(changed&&noticesVisible)renderNotices();}catch{}finally{noticeLoading=false;}}

function renderNav(){if(!me){noticeState={items:[],unreadCount:0,total:0};bookingFilter='';}nav.replaceChildren(button('Buscar empresas',()=>businessDirectory(),'quiet'));if(me){if(!pendingEmail())nav.append(button(isTeam()?'Meu espaço':'Meus agendamentos',()=>isTeam()?workspace():mine(),'quiet'),...(isTeam()&&me.roles.includes('Client')?[button('Meus agendamentos',mine,'quiet')]:[]),noticeNav());nav.append(button('Minha conta',account,'quiet'),button('Suporte',support,'quiet'),button('Sair',async()=>{await api('/auth/logout',{method:'POST',body:{}});me=null;csrf='';renderNav();login();},'quiet small'));}else nav.append(button('Entrar',()=>login(),'quiet'),...((info.emailReady||info.testMode)?[button('Criar conta',register,'primary small')]:[]));}
function modal(title,content){dialogContent.replaceChildren(stack(el('div',{class:'between'},heading(title),button('Fechar',()=>dialog.close(),'quiet small')),content));if(!dialog.open)dialog.showModal();}
function whatsAppNumber(value){
 const raw=(value??'').trim(), digits=raw.replace(/\D/g,'');
 const phone=!raw.startsWith('+')&&[10,11].includes(digits.length)?'55'+digits:digits;
 return /^[1-9]\d{9,14}$/.test(phone)?phone:null;
}
async function support(){
 const contacts=await api('/support');
 const actions=row();
 if(contacts.whatsApp)actions.append(el('a',{href:'https://wa.me/'+contacts.whatsApp,target:'_blank',rel:'noopener noreferrer',class:'support-link'},'WhatsApp'));
 if(contacts.email)actions.append(el('a',{href:'mailto:'+encodeURIComponent(contacts.email),class:'support-link'},'E-mail'));
 const card=el('div',{class:'card stack'},actions.childElementCount?actions:p('Os canais de suporte ainda estão em configuração.'));
 if(isOwner()){
  const phone=field('WhatsApp com país e DDD','tel',contacts.whatsApp,false),email=field('E-mail de suporte','email',contacts.email,false);
  card.append(form([phone.root,email.root],'Salvar canais de suporte',async()=>{await api('/support',{method:'PUT',body:{whatsApp:phone.input.value,email:email.input.value}});await support();toast('Canais de suporte atualizados.');}));
 }
 setView(el('section',{class:'auth stack'},el('h1',{},'Suporte'),card));
}
function login(after=null){
 const email=field('E-mail ou usuário','text'),password=field('Senha','password');
 password.input.autocomplete='current-password';password.input.setAttribute('aria-label','Senha');email.input.autocomplete='username';
 const submit=form([email.root,password.root],'Entrar no meu espaço',async()=>{me=await api('/auth/login',{method:'POST',body:{login:email.input.value,password:password.input.value}});password.input.value='';csrf='';renderNav();await(pendingEmail()?account():after?after():isTeam()?workspace():business?booking():businessDirectory());});
 const show=button('Mostrar',()=>{const visible=password.input.type==='password';password.input.type=visible?'text':'password';show.textContent=visible?'Ocultar':'Mostrar';show.setAttribute('aria-pressed',String(visible));},'quiet login-show');
 show.setAttribute('aria-label','Mostrar ou ocultar senha');show.setAttribute('aria-pressed','false');password.root.classList.add('login-password');password.root.append(show);
 const visual=el('div',{class:'login-story'},el('span',{class:'pill'},'SEU TEMPO, BEM CUIDADO'),el('h1',{},'Seus próximos momentos.',el('span',{},' Todos aqui.')),p('Entre para reservar um horário e acompanhar seus agendamentos com tranquilidade.','login-lead'),el('div',{class:'login-journey'},...['Escolha seu profissional','Encontre seu horário','Tenha tempo para você'].map((text,i)=>el('div',{},el('span',{},String(i+1).padStart(2,'0')),p(text)))),p('Agenda e cuidado em sintonia.','login-signature'));
 const box=el('div',{class:'login-box'},el('div',{class:'login-form-content'},el('div',{class:'login-brand'},el('img',{src:'/orbe.svg',width:36,height:36,alt:''}),el('span',{},'orbe')),p('BEM-VINDA AO SEU ESPAÇO','eyebrow'),el('h2',{},'Que bom ter você aqui.'),p('Use seu e-mail e senha para continuar.'),submit,p('Clientes entram com e-mail. A equipe também pode usar o nome de usuário.','caption'),button('Esqueci minha senha',recover,'quiet login-recover'),button('Criar conta profissional',()=>register(true),'quiet login-recover'),...((info.emailReady||info.testMode)?[el('div',{class:'login-register'},p('Sua primeira vez no Orbe?'),button('Criar minha conta',register,'quiet'))]:[]),p('Seus dados e agendamentos ficam no seu espaço pessoal.','caption login-security')));
 setView(el('section',{class:'login-scene'},visual,box));
}
function register(professional=false){professional=professional===true;if(!info.emailReady&&!info.testMode)return setView(el('section',{class:'auth card stack'},heading('Novos cadastros em preparação'),p('O cadastro de clientes será liberado em breve. Entre em contato com a equipe para agendar.'),button('Voltar',()=>booking(),'primary')));const invite=field('Código de convite da empresa'),username=field('Usuário profissional');const name=field('Nome completo'),phone=field('WhatsApp','tel'),email=field('E-mail','email'),birth=field('Data de nascimento','date'),password=field('Senha · mínimo 10 caracteres','password');password.input.minLength=10;password.input.autocomplete='new-password';birth.input.max=today();setView(el('section',{class:'auth stack'},p('UM ESPAÇO PARA VOCÊ','eyebrow'),el('h1',{},professional?'Solicite seu acesso profissional':'Crie sua conta'),p(professional?'Confirme seu e-mail após o cadastro e aguarde a aprovação do administrador da empresa.':'Seus dados ficam acessíveis apenas a você e à equipe que atende você.'),el('div',{class:'card'},form([...(professional?[invite.root,username.root]:[]),name.root,phone.root,email.root,birth.root,password.root,p('Use letras maiúsculas, minúsculas e números na senha.','caption')],'Criar minha conta',async()=>{const r=await api('/auth/register',{method:'POST',body:{fullName:name.input.value,phone:phone.input.value,email:email.input.value,birthDate:birth.input.value,password:password.input.value,...(professional?{invitationCode:invite.input.value,username:username.input.value}:{})}});if(r.emailConfirmationRequired===false){toast(r.message);return login();}setView(el('section',{class:'auth card stack'},el('span',{class:'pill'},'Confira seu e-mail'),heading('Falta só confirmar'),p(r.message),button('Entrar na minha conta',()=>login(),'primary')));}))));}
function recover(){if(!info.emailReady)return setView(el('section',{class:'auth card stack'},heading('Vamos recuperar seu acesso'),p('A recuperação por e-mail ainda está em preparação. Entre em contato com a administração para obter ajuda.'),button('Voltar para entrar',()=>login(),'primary')));const email=field('E-mail da sua conta','email');setView(el('section',{class:'auth card stack'},heading('Vamos recuperar seu acesso'),p('Enviaremos um link para você definir uma nova senha.'),form([email.root],'Enviar instruções',async()=>{const r=await api('/auth/recover',{method:'POST',body:{email:email.input.value}});toast(r.message);}),button('Voltar para entrar',()=>login(),'quiet')));}
function bookingUsernameForm(){
 if(me.bookingUsername)return el('div',{class:'stack'},heading('Usuário para agendamentos fixos'),p(me.bookingUsername),p('Compartilhe este usuário com a profissional.'));
 const username=field('Nome de usuário','text');
 username.input.minLength=3;username.input.maxLength=32;
 return el('details',{open:!me.bookingUsername},el('summary',{},'Usuário para agendamentos fixos'),p('Compartilhe seu nome de usuário com a profissional que fará seu agendamento fixo.'),form([username.root],'Salvar nome de usuário',async()=>{me=await api('/auth/booking-username',{method:'PUT',body:{username:username.input.value}});await account();toast('Nome de usuário salvo.');}));
}
async function account(){if(!me)return login(account);if(pendingEmail())return setView(el('section',{class:'auth card stack'},el('h1',{},'Minha conta'),heading('Confirme seu e-mail'),p('Confirme '+me.email+' para liberar seus agendamentos.'),...(info.emailReady?[button('Reenviar confirmação de e-mail',async()=>toast((await api('/auth/resend',{method:'POST',body:{}})).message),'primary')]:[])));const name=field('Nome completo','text',me.fullName),phone=field('WhatsApp','tel',me.phone),birth=field('Data de nascimento','date',me.birthDate??'',me.roles.includes('Client'));birth.input.max=today();setView(el('section',{class:'auth stack'},p('SEUS DADOS','eyebrow'),el('h1',{},'Minha conta'),el('div',{class:'card stack'},await membershipAccount(),form([name.root,phone.root,birth.root],'Salvar alterações',async()=>{me=await api('/auth/me',{method:'PUT',body:{fullName:name.input.value,phone:phone.input.value,birthDate:birth.input.value||null}});toast('Cadastro atualizado.');}),el('hr',{class:'hr'}),p(me.email),...(me.roles.includes('Client')&&me.emailConfirmed?[bookingUsernameForm()]:[]),button('Alterar e-mail',()=>{const address=field('Novo e-mail','email'),password=field('Senha atual','password');modal('Alterar e-mail',form([address.root,password.root],'Enviar confirmação',async()=>{const r=await api('/auth/email',{method:'POST',body:{email:address.input.value,password:password.input.value}});dialog.close();toast(r.message);}));}),button('Trocar senha',()=>{const current=field('Senha atual','password'),next=field('Nova senha','password');next.input.minLength=10;modal('Trocar senha',form([current.root,next.root],'Trocar senha',async()=>{await api('/auth/password',{method:'POST',body:{currentPassword:current.input.value,newPassword:next.input.value}});dialog.close();me=null;csrf='';renderNav();login();toast('Senha alterada. Entre novamente.');}));}))));}
async function businessDirectory(){
 if(!me)return login(businessDirectory);
 const search=field('Nome da empresa','search','',false),results=stack();
 const load=async()=>{const items=await api('/public/businesses?q='+encodeURIComponent(search.input.value));results.replaceChildren(...items.map(b=>el('a',{class:'support-link',href:'/'+b.slug},b.name)));if(!items.length)results.append(p('Nenhuma empresa encontrada.'));};
 setView(el('section',{class:'auth stack'},el('h1',{},'Encontre uma empresa'),form([search.root],'Buscar',load),results));
 await load();
}
async function booking(){
 if(!me)return login(booking);
 if(pendingEmail())return account();
 info=await api('/public/info');
 const shell=el('section',{class:'booking-shell'}),aside=el('aside',{class:'booking-aside'}),panel=el('div',{class:'booking-panel card'});
 const progress=el('div',{class:'booking-progress','aria-live':'polite'}),content=el('div',{class:'booking-content'}),feedback=el('p',{class:'booking-error',role:'alert',hidden:true}),actions=el('div',{class:'booking-actions'});
 panel.append(progress,content,feedback,actions);shell.append(aside,panel);setView(shell);
 if(!info.publicBookingOpen){aside.append(p('SEU PRÓXIMO MOMENTO','eyebrow'),el('h1',{},info.name));content.append(heading('Em breve, novos horários por aqui'),p('As reservas online ainda não estão abertas. Entre em contato com a equipe para agendar.'),button('Acessar minha conta',()=>isTeam()?workspace():mine(),'primary'));return;}
 let people=[],catalog=[],catalogEmployee='',category='',stage='professional',busy=false,request=0;
 const cache=new Map(),positions=new Map();let next,previous;
 const labels={professional:'Profissional',category:'Categoria',service:'Serviço',datetime:'Data e horário',confirm:'Confirmar'};
 const stages=()=>['professional',...(catalog.some(s=>s.categoryId)?['category']:[]),'service','datetime','confirm'];
 const categoryOf=s=>s.categoryId??'other';
 const ready=()=>stage==='professional'?!!state.employee:stage==='category'?!!category:stage==='service'?!!state.service:stage==='datetime'?!!state.slot:true;
 const fail=error=>{feedback.hidden=false;feedback.textContent=error.message??String(error);};
 function clearTime(){state.day=null;state.slot=null;state.key=null;state.calendarMonth=null;state.calendarView='month';}
 function choose(kind,item){
  if(kind==='professional'){
   if(state.employee?.id!==item.id){state.employee=item;state.service=null;category='';catalog=[];catalogEmployee='';cache.clear();clearTime();}
  }else if(kind==='category'){
   if(category!==item.id){category=item.id;if(state.service&&categoryOf(state.service)!==category){state.service=null;clearTime();}}
  }else if(state.service?.id!==item.id){state.service=item;clearTime();}
  for(const option of content.querySelectorAll('.booking-option')){const selected=option.dataset.choice===item.id;option.classList.toggle('selected',selected);option.setAttribute('aria-pressed',String(selected));}
  feedback.hidden=true;updateSummary();updateActions();
 }
 function option(kind,item,selected,detail=''){
  const b=el('button',{type:'button',class:'booking-option'+(selected?' selected':''),'aria-pressed':String(selected),'data-choice':item.id,onclick:()=>choose(kind,item)},
   el('span',{class:'booking-option-mark','aria-hidden':'true'},kind==='professional'?item.displayName.trim().slice(0,1).toUpperCase():'✓'),
   el('span',{class:'booking-option-copy'},el('strong',{},item.displayName??item.name),detail?el('span',{},detail):null));return b;
 }
 function summaryItem(label,value,target){return el('div',{class:'booking-summary-item'},el('div',{},p(label,'caption'),el('strong',{},value)),target?button('Alterar',()=>go(target),'quiet small'):null);}
 function updateSummary(){
  aside.replaceChildren();shell.dataset.stage=stage;
  if(stage==='professional'){
   aside.append(p('CUIDADO QUE CABE NA SUA ROTINA','eyebrow'),el('h1',{},info.name),p('Escolha quem vai cuidar de você. Nos próximos passos, encontre o serviço e o melhor horário.','lead'),el('div',{class:'booking-assurance'},IconText('01','Escolha seu profissional'),IconText('02','Encontre seu horário'),IconText('03','Confira e confirme')));return;
  }
  const details=el('details',{class:'booking-summary',open:window.matchMedia('(min-width:1001px)').matches});
  details.append(el('summary',{},'Resumo da reserva'),el('h2',{},info.name));
  if(state.employee)details.append(summaryItem('Profissional',state.employee.displayName,'professional'));
  if(category&&catalog.some(s=>s.categoryId)){const item=catalog.find(s=>categoryOf(s)===category);details.append(summaryItem('Categoria',category==='other'?'Outros serviços':item?.categoryName??'Categoria','category'));}
  if(state.service)details.append(summaryItem('Serviço',state.service.name,'service'),summaryItem('Duração',state.service.durationMinutes+' minutos'),summaryItem(state.service.priceFrom?'Valor a partir de':'Valor',money(state.service.priceCents)));
  if(state.slot)details.append(summaryItem('Quando',stamp(state.slot.start),'datetime'));
  details.append(p('Seus agendamentos ficam disponíveis na sua conta.','caption'));aside.append(details);
 }
 function IconText(number,label){return el('div',{class:'booking-assurance-item'},el('span',{},number),el('strong',{},label));}
 function updateActions(){
  actions.replaceChildren();const order=stages(),index=order.indexOf(stage);
  previous=el('button',{type:'button',class:'quiet',disabled:busy||index===0,onclick:()=>go(order[index-1])},'Voltar');
  next=el('button',{type:'button',class:'primary',disabled:busy||!ready(),onclick:advance},busy?'Aguarde…':stage==='confirm'?'Confirmar meu agendamento':stage==='datetime'?'Revisar reserva':'Continuar');
  actions.append(previous,next);
 }
 async function advance(){
  if(busy||!ready())return;busy=true;feedback.hidden=true;updateActions();
  try{
   if(stage==='professional'&&catalogEmployee!==state.employee.id){
    const id=state.employee.id;const services=await api('/public/services?employeeId='+encodeURIComponent(id));
    if(!shell.isConnected||state.employee.id!==id)return;
    catalog=services;catalogEmployee=id;category=state.service?categoryOf(state.service):'';
   }
   if(stage==='confirm'){
    if(!me.roles.includes('Client'))throw Error('Use uma conta de cliente para confirmar a reserva.');
    const record=await api('/bookings',{method:'POST',key:state.key,body:{serviceId:state.service.id,employeeId:state.employee.id,start:state.slot.start}});
    state.slot=null;state.key=null;toast('Seu horário está confirmado: '+stamp(record.start));if(shell.isConnected)await mine();return;
   }
   const order=stages();await go(order[order.indexOf(stage)+1]);
  }catch(error){
   if(error.status===401)return login(booking);
   if(error.status===409&&stage==='confirm'){cache.clear();state.slot=null;state.key=null;await go('datetime');}
   fail(error);
  }finally{busy=false;if(shell.isConnected)updateActions();}
 }
 async function go(target){
  if(!target||!shell.isConnected)return;
  positions.set(stage,window.scrollY);stage=target;const ticket=++request;
  feedback.hidden=true;content.replaceChildren();updateSummary();
  const order=stages(),index=order.indexOf(stage);
  progress.replaceChildren(p('Etapa '+(index+1)+' de '+order.length,'caption'),el('div',{class:'booking-step-track'},...order.map((key,i)=>el('span',{class:i<=index?'complete':'','aria-current':key===stage?'step':null},el('b',{},i+1),labels[key]))));
  const headingText={professional:'Quem vai atender você?',category:'Que tipo de cuidado?',service:'Escolha seu serviço',datetime:'Escolha o dia e o horário',confirm:'Está tudo certo?'};
  content.append(el('h2',{tabIndex:-1},headingText[stage]));
  if(stage==='professional'){
   content.append(p('Selecione um profissional para continuar.','caption'));
   const list=el('div',{class:'booking-options'});for(const person of people)list.append(option('professional',person,person.id===state.employee?.id,'Ver serviços e horários'));content.append(list);
   if(!people.length)content.append(p('Ainda não há profissionais disponíveis. Entre em contato com a empresa.'));
  }else if(stage==='category'){
   const categories=[...new Map(catalog.filter(s=>s.categoryId).map(s=>[s.categoryId,s.categoryName]))].map(([id,name])=>({id,name}));
   if(catalog.some(s=>!s.categoryId))categories.push({id:'other',name:'Outros serviços'});
   const list=el('div',{class:'booking-options'});for(const item of categories)list.append(option('category',item,item.id===category));content.append(list);
  }else if(stage==='service'){
   const services=catalog.filter(s=>!catalog.some(x=>x.categoryId)||categoryOf(s)===category),list=el('div',{class:'booking-options'});
   for(const item of services)list.append(option('service',item,item.id===state.service?.id,item.durationMinutes+' min · '+servicePrice(item.priceCents,item.priceFrom)));content.append(list);
   if(!services.length)content.append(p('Não há serviços disponíveis nesta seleção. Volte para escolher outra opção.'));
  }else if(stage==='datetime')await renderCalendar(ticket);
  else{
   content.append(p('Confira sua escolha antes de confirmar.','caption'),el('div',{class:'booking-review'},summaryItem('Empresa',info.name),summaryItem('Profissional',state.employee.displayName),summaryItem('Serviço',state.service.name),summaryItem('Quando',stamp(state.slot.start)),summaryItem('Duração',state.service.durationMinutes+' minutos'),summaryItem(state.service.priceFrom?'Valor a partir de':'Valor',money(state.service.priceCents))),p('Após reservar, você pode cancelar em 24 horas, ou em 2 horas se o atendimento for no dia seguinte, sempre antes do início. Agendar não representa pagamento.','caption'));
  }
  if(!shell.isConnected||ticket!==request)return;updateActions();content.querySelector('h2')?.focus({preventScroll:true});
  if(positions.has(stage))window.scrollTo({top:positions.get(stage),behavior:'instant'});
 }
 async function renderCalendar(ticket){
  const date=today(),first=(state.calendarMonth??date.slice(0,7))+'-01',month=new Date(first+'T12:00:00Z'),last=new Date(Date.UTC(month.getUTCFullYear(),month.getUTCMonth()+1,0,12));
  const controls=row(),calendar=el('div',{class:'booking-calendar'}),times=el('div',{class:'booking-times'}),layout=el('div',{class:'booking-date-layout'},calendar,times);content.append(layout);
  const monthName=new Intl.DateTimeFormat('pt-BR',{month:'long',year:'numeric',timeZone:'UTC'}).format(month),mode=select('Visualização',[{id:'month',name:'Mês'},{id:'year',name:'Ano'}],state.calendarView??'month');
  async function move(delta){const value=new Date(month);value.setUTCMonth(value.getUTCMonth()+delta);if(value.getUTCFullYear()<1900||value.getUTCFullYear()>2100)return;state.calendarMonth=value.toISOString().slice(0,7);state.day=state.slot=state.key=null;await go('datetime');}
  controls.append(button('‹',()=>move(state.calendarView==='year'?-12:-1),'small'),button('Hoje',async()=>{state.calendarMonth=date.slice(0,7);state.day=state.slot=state.key=null;await go('datetime');},'small'),button('›',()=>move(state.calendarView==='year'?12:1),'small'));
  mode.input.onchange=()=>{state.calendarView=mode.input.value;go('datetime').catch(fail);};calendar.append(el('div',{class:'between calendar-toolbar'},heading(state.calendarView==='year'?String(month.getUTCFullYear()):monthName),controls),mode.root);
  if(state.calendarView==='year'){
   const grid=el('div',{class:'calendar-year'});for(let m=0;m<12;m++){const name=new Intl.DateTimeFormat('pt-BR',{month:'long',timeZone:'UTC'}).format(new Date(Date.UTC(month.getUTCFullYear(),m,1)));grid.append(button(name,async()=>{state.calendarMonth=month.getUTCFullYear()+'-'+String(m+1).padStart(2,'0');state.calendarView='month';state.day=state.slot=state.key=null;await go('datetime');},'month-choice'));}calendar.append(grid);times.append(p('Escolha um mês para consultar os horários.'));return;
  }
  times.append(p('Consultando horários…','caption'));const key=state.employee.id+'|'+state.service.id+'|'+first;
  let result=cache.get(key);if(!result){result=await api('/public/availability?employeeId='+encodeURIComponent(state.employee.id)+'&serviceId='+encodeURIComponent(state.service.id)+'&from='+first+'&days='+last.getUTCDate());if(ticket!==request||!shell.isConnected)return;cache.set(key,result);}
  times.replaceChildren();if(!result.open){state.slot=null;times.append(p('A agenda foi fechada pela equipe. Consulte a empresa.'));return;}
  const grid=el('div',{class:'days',role:'group','aria-label':'Dias de '+monthName});calendar.append(grid);
  for(const name of ['DOM','SEG','TER','QUA','QUI','SEX','SÁB'])grid.append(el('span',{class:'weekday'},name));for(let i=0;i<month.getUTCDay();i++)grid.append(el('span',{'aria-hidden':'true'}));
  function showTimes(day){
   times.replaceChildren();const title=new Intl.DateTimeFormat('pt-BR',{dateStyle:'full',timeZone:'UTC'}).format(new Date(day.date+'T12:00:00Z'));times.append(el('h3',{},title));
   for(const [name,from,until]of [['Manhã',0,12],['Tarde',12,18],['Noite',18,24]]){
    const slots=day.slots.filter(s=>{const hour=Number(time(s.start).split(':')[0]);return hour>=from&&hour<until;});if(!slots.length)continue;
    const group=el('div',{class:'booking-time-group'},el('strong',{},name)),list=el('div',{class:'slots'});group.append(list);times.append(group);
    for(const slot of slots){const chosen=state.slot?.start===slot.start;const b=el('button',{type:'button',class:'slot'+(chosen?' selected':''),'aria-pressed':String(chosen),onclick:()=>{
     if(state.slot?.start!==slot.start){state.slot=slot;state.key=crypto.randomUUID();}
     times.querySelectorAll('.slot').forEach(x=>{const active=x===b;x.classList.toggle('selected',active);x.setAttribute('aria-pressed',String(active));});updateSummary();updateActions();
    }},time(slot.start));list.append(b);}
   }
  }
  for(const day of result.days){const b=el('button',{type:'button',class:'day'+(state.day===day.date?' selected':''),disabled:!day.slots.length,'aria-label':day.date+(day.slots.length?', horários disponíveis':', sem horários disponíveis'),onclick:()=>{
   if(state.day!==day.date){state.day=day.date;state.slot=state.key=null;}grid.querySelectorAll('button').forEach(x=>x.classList.toggle('selected',x===b));showTimes(day);updateSummary();updateActions();
  }},String(Number(day.date.slice(-2))));grid.append(b);}
  const selected=result.days.find(d=>d.date===state.day&&d.slots.length);if(selected){if(state.slot&&!selected.slots.some(s=>s.start===state.slot.start)){state.slot=state.key=null;}showTimes(selected);}
  else times.append(p(result.days.some(d=>d.slots.length)?'Escolha um dia no calendário para ver os horários.':'Não há horários disponíveis neste mês. Consulte outro período ou profissional.'));
 }
 updateSummary();content.append(p('Carregando profissionais…','caption'));
 try{people=await api('/public/employees');if(!people.some(p=>p.id===state.employee?.id)){state.employee=state.service=null;clearTime();}await go('professional');}catch(error){fail(error);content.append(button('Tentar novamente',booking));}
}

async function mine(){if(!me)return login(mine);const companies=await api('/bookings/businesses');const path='/bookings/mine'+(bookingFilter?'?business='+encodeURIComponent(bookingFilter):'');const items=await allPages(path,100);const company=select('Empresa',[{id:'',name:'Todas as empresas'},...companies.map(x=>({id:x.slug,name:x.name}))],bookingFilter);company.input.onchange=()=>{bookingFilter=company.input.value;mine().catch(e=>toast(e.message,true));};const list=el('div',{class:'list'});setView(stack(el('div',{class:'title'},el('div',{},p('SEUS PRÓXIMOS MOMENTOS','eyebrow'),el('h1',{},'Meus agendamentos')),button('+ Agendar',businessDirectory,'primary')),company.root,list));main.dataset.live='mine';main.dataset.signature=JSON.stringify(items);if(!items.length)list.append(el('div',{class:'empty'},'Sua agenda está livre. Que tal reservar seu próximo momento?'));for(const item of items){const actions=row();const card=el('article',{class:'booking'},el('div',{},el('span',{class:'pill'},item.status==='Confirmed'?'Confirmado':'Cancelado'),el('h3',{},item.title),p((item.businessName??'Orbe')+' · '+(item.employeeName??'Equipe')+' · '+stamp(item.start,item.timeZoneId),'caption'),p(servicePrice(item.expectedCents,item.priceFrom),'caption')),actions);if(item.status==='Confirmed'){if(item.serviceId)actions.append(button('Remarcar',()=>reschedule(item),'small'));if(item.canCancel===false&&whatsAppNumber(item.professionalPhone))actions.append(el('a',{href:'https://wa.me/'+whatsAppNumber(item.professionalPhone),target:'_blank',rel:'noopener noreferrer',class:'support-link'},'WhatsApp do profissional'));if(item.canCancel!==false)actions.append(button('Cancelar',()=>modal('Cancelar agendamento?',stack(p(item.title+' · '+stamp(item.start,item.timeZoneId)),p('O horário ficará disponível para outra pessoa.'),button('Confirmar cancelamento',async()=>{await bookingApi(item,'/bookings/'+item.id+'/cancel',{method:'POST',body:{version:item.version}});dialog.close();await mine();toast('Agendamento cancelado.');},'danger'))),'danger small'));}list.append(card);}}
async function reschedule(item){const date=field('Novo dia','date',new Intl.DateTimeFormat('sv-SE',{timeZone:item.timeZoneId??info.timeZoneId}).format(new Date())),slots=el('div',{class:'slots'});const content=stack(p('Serviço e profissional serão mantidos.'),date.root,button('Consultar horários',async()=>{const result=await bookingApi(item,`/bookings/${item.id}/availability?from=${date.input.value}&days=1`);slots.replaceChildren();for(const slot of result.days[0]?.slots??[])slots.append(button(time(slot.start,item.timeZoneId),async()=>{await bookingApi(item,'/bookings/'+item.id+'/move',{method:'POST',body:{version:item.version,start:slot.start}});dialog.close();await mine();toast('Agendamento remarcado.');},'slot'));if(!slots.childElementCount)slots.append(p('Nenhum horário disponível neste dia.'));}),slots);modal('Escolha o novo horário',content);}
let teamEmployee='',teamTab='agenda';
async function workspace(tab=teamTab){if(!isTeam())return login(()=>workspace(tab));teamTab=tab;const shell=stack(el('div',{class:'title'},el('div',{},p(isOwner()?'VISÃO DO SEU NEGÓCIO':'SEU ESPAÇO DE TRABALHO','eyebrow'),el('h1',{},'Olá, '+me.fullName.split(' ')[0])),button('Minha conta',account)),el('div',{class:'tabs'},...['agenda','financeiro','disponibilidade','serviços',...(isOwner()?['equipe','configurações','importação']:[])].map(t=>button(t[0].toUpperCase()+t.slice(1),()=>workspace(t),t===tab?'active':''))));const body=el('div',{class:'stack'});shell.append(body);setView(shell);const companies=await api('/businesses/mine');if(companies.length){const company=select('Empresa',[{id:'',name:'Selecione a empresa'},...companies.map(x=>({id:x.slug,name:x.name}))],business||'orbe');shell.prepend(company.root);company.input.onchange=async()=>{if(!company.input.value)return;business=company.input.value;teamEmployee=null;noticeState={items:[],unreadCount:0,total:0};me=await api('/auth/me');info=await api('/public/info');renderNav();await workspace('agenda');};if(!companies.some(x=>x.slug===(business||'orbe'))){body.append(p('Selecione uma empresa para abrir o espaço de trabalho.'));return;}}if(['agenda','financeiro','disponibilidade'].includes(tab)){const people=(await api('/team/employees')).filter(x=>isOwner()||tab!=='disponibilidade'||x.enabled);if(!people.length){body.append(el('div',{class:'empty'},'Cadastre um profissional na área Equipe para começar.'));return;}if(!people.some(x=>x.id===teamEmployee))teamEmployee=(people.find(x=>x.enabled)??people[0]).id;const who=select('Profissional',people.map(x=>({id:x.id,name:x.displayName+(x.enabled?'':' · inativo')})),teamEmployee);who.input.onchange=()=>{teamEmployee=who.input.value;workspace(tab).catch(e=>toast(e.message,true));};body.append(who.root);}if(tab==='agenda')await teamAgenda(body);else if(tab==='financeiro')await finances(body);else if(tab==='disponibilidade')await hours(body);else if(tab==='equipe')await employees(body);else if(tab==='serviços')await services(body);else if(tab==='configurações')await settings(body);else await imports(body);}
async function teamAgenda(host){const items=await allPages('/team/appointments?employeeId='+teamEmployee);main.dataset.live='agenda';main.dataset.signature=JSON.stringify(items);host.append(button('+ Bloquear período',()=>blockForm(),'primary'));await birthdaysPanel(host);if(!items.length)host.append(el('div',{class:'empty'},'Nenhum compromisso nesta agenda.'));for(const item of items){const actions=row();if(item.status==='Confirmed'&&item.kind==='Atendimento')actions.append(button(item.receiptId?'Recebimento registrado · editar':'Lançar recebimento',()=>moneyForm(item),'small'),button('Cancelar',()=>modal('Cancelar atendimento?',stack(p(item.title),p('O horário será liberado. Histórico e lançamentos financeiros serão preservados.'),button('Confirmar cancelamento',async()=>{await api('/bookings/'+item.id+'/cancel',{method:'POST',body:{version:item.version}});dialog.close();await workspace();},'danger'))),'quiet small'));actions.append(button('Excluir',()=>modal('Excluir agendamento e lançamentos?',stack(p(item.title),p('Todos os lançamentos vinculados também serão excluídos. Lançamentos avulsos serão mantidos.'),button('Excluir tudo vinculado',async()=>{await api('/team/appointments/'+item.id,{method:'DELETE',body:{version:item.version}});dialog.close();await workspace();},'danger'))),'danger small'));host.append(el('article',{class:'booking'},el('div',{},el('span',{class:'pill'},item.kind+' · '+(item.status==='Pending'?'Aguardando remarcação':item.status==='Confirmed'?'Confirmado':'Cancelado')),el('h3',{},item.title),p(stamp(item.start)+' — '+time(item.end),'caption'),p(item.clientName??'','caption')),actions));}}
function blockForm(){
 const dates=dateSelection(),batchId=crypto.randomUUID();
 const title=field('Título'),kind=select('Tipo',[{id:'Folga',name:'Folga'},{id:'Compromisso',name:'Compromisso'},{id:'Outro',name:'Outro'}],'Folga');
 const scope=select('Período',[{id:'all',name:'Dia inteiro'},{id:'morning',name:'Manhã'},{id:'afternoon',name:'Tarde'},{id:'custom',name:'Escolher horários'}],'all');
 const from=field('Início','time','09:00'),until=field('Fim','time','12:00'),times=el('div',{class:'grid2'},from.root,until.root);
 function update(){times.hidden=scope.input.value==='all';from.input.disabled=until.input.disabled=times.hidden;
  if(scope.input.value==='morning'){from.input.value='09:00';until.input.value='12:00';}
  if(scope.input.value==='afternoon'){from.input.value='13:00';until.input.value='18:00';}
 }
 scope.input.onchange=update;update();
 modal('Folga ou compromisso',form([title.root,kind.root,dates.root,scope.root,times,p('Confira as datas selecionadas. Se houver conflito, nenhum bloqueio será gravado.','caption')],'Salvar bloqueios',async()=>{
  const selected=dates.dates(),all=scope.input.value==='all';
  if(!window.confirm('Criar '+kind.input.value+' em '+selected.length+' data(s), '+(all?'dia inteiro':from.input.value+' até '+until.input.value)+'?'))return;
  await api('/team/blocks/batch',{method:'POST',body:{batchId,employeeId:teamEmployee,title:title.input.value,kind:kind.input.value,dates:selected,allDay:all,startTime:from.input.value+':00',endTime:until.input.value+':00'}});
  dialog.close();toast('Bloqueios salvos em '+selected.length+' data(s).');await workspace();
 }));
}
async function moneyForm(appointment=null,scope=business){
 const employeeId=appointment?.employeeId??teamEmployee;
 const existing=appointment?(await allEntries(scope,employeeId)).find(e=>e.appointmentId===appointment.id&&e.kind==='Receita'):null;
 if(existing)return editMoney(existing,scope);
 const id=crypto.randomUUID(),title=field('Descrição','text',appointment?.title??''),amount=field('Valor recebido · R$','text',appointment&&!appointment.priceFrom?(appointment.expectedCents/100).toFixed(2):''),date=field('Data','date',today()),kind=select('Tipo',[{id:'Receita',name:'Recebimento'},{id:'Despesa',name:'Despesa'}],'Receita');
 let link;
 if(appointment)link=select('Agendamento',[{id:appointment.id,name:appointment.title}],appointment.id);
 else{const items=await allPages('/team/appointments?employeeId='+employeeId);link=select('Agendamento vinculado · opcional',[{id:'',name:'Sem vínculo'},...items.filter(x=>x.kind==='Atendimento'&&x.status==='Confirmed').map(x=>({id:x.id,name:stamp(x.start)+' · '+x.title}))]);}
 modal('Registrar recebimento',form([title.root,amount.root,kind.root,date.root,link.root,p('Cada atendimento aceita um recebimento. Um lançamento já registrado pode ser editado.','caption')],'Salvar lançamento',async()=>{
  await api('/team/entries',{method:'POST',scope,body:{id,employeeId,appointmentId:link.input.value||null,description:title.input.value,kind:kind.input.value,date:date.input.value,cents:cents(amount.input.value)}});dialog.close();toast('Recebimento registrado.');await refreshMoneyScope(scope);
 }));
}
async function allEntries(scope=business,employeeId=teamEmployee){const rows=[];for(let offset=0;;offset+=200){const page=await api('/team/entries?employeeId='+encodeURIComponent(employeeId??'')+'&offset='+offset,{scope});rows.push(...page);if(page.length<200)return rows;}}
async function refreshMoneyScope(scope){await pollNotices();if(scope===business&&isTeam())await workspace();}
async function editMoney(entry,scope=business){
 const title=field('Descrição','text',entry.description),amount=field('Valor recebido · R$','text',(entry.cents/100).toFixed(2)),date=field('Data','date',entry.date);
 modal('Editar lançamento',form([p(entry.automatic?'Recebimento registrado automaticamente':'Lançamento registrado'),title.root,amount.root,date.root],'Salvar alterações',async()=>{
  await api('/team/entries/'+entry.id,{method:'PUT',scope,body:{version:entry.version,description:title.input.value,date:date.input.value,cents:cents(amount.input.value)}});dialog.close();await refreshMoneyScope(scope);
 }));
}
async function finances(host){
 const rows=await allEntries();main.dataset.live='finance';main.dataset.signature=JSON.stringify(rows);
 const income=rows.filter(e=>e.kind==='Receita').reduce((sum,e)=>sum+e.cents,0),expense=rows.filter(e=>e.kind==='Despesa').reduce((sum,e)=>sum+e.cents,0);
 host.append(heading('Movimentações financeiras'),p('Recebimentos '+money(income)+' · Despesas '+money(expense)+' · Resultado '+money(income-expense)),button('+ Novo lançamento',()=>moneyForm(),'primary'));
 for(const entry of rows)host.append(el('article',{class:'booking'},el('div',{},el('h3',{},entry.description),p(entry.date+' · '+entry.kind+(entry.automatic?' · Automático':'')),p(money(entry.cents))),row(button('Editar',()=>editMoney(entry),'small'),button('Excluir',()=>modal('Excluir lançamento?',stack(p('Um recebimento excluído não será recriado automaticamente.'),button('Confirmar exclusão',async()=>{await api('/team/entries/'+entry.id,{method:'DELETE',body:{version:entry.version}});dialog.close();await workspace();},'danger'))),'danger small'))));
 if(!rows.length)host.append(p('Nenhum lançamento registrado.'));
}
async function birthdaysPanel(host){
 const year=field('Ano dos aniversários','number',today().slice(0,4)),month=select('Mês',Array.from({length:12},(_,i)=>({id:String(i+1),name:new Intl.DateTimeFormat('pt-BR',{month:'long',timeZone:'UTC'}).format(new Date(Date.UTC(2026,i,1)))})),String(Number(today().slice(5,7))));
 const content=stack(),panel=el('details',{class:'card stack'},el('summary',{},'Aniversários das clientes'),p('Usamos a data cadastrada na conta. Aniversários não ocupam horários nem geram valores.','caption'),el('div',{class:'grid2'},year.root,month.root),content);host.append(panel);
 async function draw(){const y=Number(year.input.value),m=Number(month.input.value);if(y<1900||y>2100)return;const dates=await api('/team/birthdays?year='+y+'&employeeId='+teamEmployee);content.replaceChildren();const grid=el('div',{class:'days birthday-calendar'}),names=stack();content.append(grid,names);
 for(const day of ['D','S','T','Q','Q','S','S'])grid.append(el('span',{},day));for(let i=0;i<new Date(Date.UTC(y,m-1,1)).getUTCDay();i++)grid.append(el('span'));
 for(let d=1;d<=new Date(Date.UTC(y,m,0)).getUTCDate();d++){const key=y+'-'+String(m).padStart(2,'0')+'-'+String(d).padStart(2,'0'),people=dates.filter(b=>b.date===key);const b=button(String(d),()=>{names.replaceChildren(heading(d+'/'+m),...people.map(p=>pNode(p.name)));if(!people.length)names.append(pNode('Sem aniversários.'));},people.length?'birthday-day':'');b.setAttribute('aria-label',d+'/'+m+'/'+y+(people.length?', '+people.length+' aniversário(s)':''));grid.append(b);}
 const selected=dates.filter(b=>Number(b.date.slice(5,7))===m);for(const person of selected)names.append(p(person.date.slice(8,10)+'/'+person.date.slice(5,7)+' · '+person.name));if(!selected.length)names.append(p('Sem aniversários neste mês.'));
 }
 const pNode=name=>p(name);year.input.onchange=month.input.onchange=()=>draw().catch(e=>toast(e.message,true));await draw();
}
const weekdays=['Domingo','Segunda','Terça','Quarta','Quinta','Sexta','Sábado'];
function minute(s){const[h,m]=s.split(':').map(Number);return h*60+m;}function hhmm(m){return String(Math.floor(m/60)).padStart(2,'0')+':'+String(m%60).padStart(2,'0');}
async function hours(host){
 const data=await api('/team/hours?employeeId='+teamEmployee),weekly=stack(),exceptions=stack(),rows=[],dayRows=[];
 function period(parent,items,label,start=540,end=720,disabled=false){const from=field('Início','time',hhmm(start)),until=field('Fim','time',hhmm(end===1440?0:end)),off=el('input',{type:'checkbox',checked:disabled}),midnight=el('input',{type:'checkbox',checked:end===1440});
 const box=el('div',{class:'period-card stack'},el('div',{class:'between'},el('strong',{},label),el('label',{class:'check'},off,'Desativado')),el('div',{class:'grid2'},from.root,until.root),el('label',{class:'check caption'},midnight,'Termina à meia-noite'));parent.append(box);
 function update(){from.input.disabled=off.checked;until.input.disabled=off.checked||midnight.checked;midnight.disabled=off.checked;box.classList.toggle('period-off',off.checked);}off.onchange=midnight.onchange=update;update();items.push({from,until,off,midnight});
 }
 function addDay(special=false,value=special?today():'1',windows=null){const box=el('div',{class:'card stack'}),day=special?field('Data especial','date',value):select('Dia da semana',weekdays.map((name,i)=>({id:String(i),name})),String(value)),periods=stack(),items=[],record={day,items,box};const collection=special?dayRows:rows;collection.push(record);box.append(day.root,periods);
 if(windows===null){period(periods,items,'Manhã',540,720);period(periods,items,'Tarde',780,1080);}else if(!windows.length){period(periods,items,'Manhã',540,720,true);period(periods,items,'Tarde',780,1080,true);}else windows.forEach((w,i)=>period(periods,items,'Período '+(i+1),w.startMinute,w.endMinute));
 box.append(row(button('+ Outro período',()=>period(periods,items,'Período extra',1080,1200),'small'),button('Remover '+(special?'data':'jornada'),()=>{collection.splice(collection.indexOf(record),1);box.remove();},'danger small')));(special?exceptions:weekly).append(box);
 }
 const groups=new Map();for(const w of data.weekly){if(!groups.has(w.day))groups.set(w.day,[]);groups.get(w.day).push(w.window);}for(const [day,windows]of groups)addDay(false,day,windows);
 host.append(heading('Jornada semanal'),p('Escolha o dia e os horários de manhã e tarde. Desative o período em que não atende.'),weekly,button('+ Jornada · manhã e tarde',()=>addDay()),heading('Datas especiais'),p('Substituem a jornada semanal. Desative todos os períodos para deixar a data indisponível.'),exceptions);
 for(const d of data.exceptions)addDay(true,d.date,d.windows);
 const windows=record=>record.items.filter(x=>!x.off.checked).map(x=>({startMinute:minute(x.from.input.value),endMinute:x.midnight.checked?1440:minute(x.until.input.value)}));
 const bulk=el('details',{class:'card stack'},el('summary',{},'Selecionar várias datas especiais')),dates=dateSelection(),editor=stack(),bulkItems=[],replace=el('input',{type:'checkbox'});
 period(editor,bulkItems,'Manhã',540,720);period(editor,bulkItems,'Tarde',780,1080);
 bulk.append(dates.root,editor,button('+ Outro período',()=>period(editor,bulkItems,'Período extra',1080,1200),'small'),el('label',{class:'check'},replace,'Substituir datas especiais já configuradas'),
  button('Aplicar às datas selecionadas',()=>{
   const selected=dates.dates(),existing=dayRows.filter(r=>selected.includes(r.day.input.value));
   if(existing.length&&!replace.checked)throw Error('Há datas especiais já configuradas. Marque Substituir ou retire essas datas.');
   const periods=windows({items:bulkItems});
   validatePeriods(periods);
   if(!window.confirm('Aplicar estes horários a '+selected.length+' data(s)?'+(existing.length?' '+existing.length+' configuração(ões) serão substituídas.':'')))return;
   for(const r of existing){dayRows.splice(dayRows.indexOf(r),1);r.box.remove();}
   for(const date of selected)addDay(true,date,periods);
   bulk.open=false;toast('Datas preparadas. Confira a lista e toque em Salvar minha disponibilidade.');
  },'primary'));
 host.append(button('+ Data especial',()=>addDay(true)),bulk,button('Salvar minha disponibilidade',async()=>{
  const employee=teamEmployee,exceptions=dayRows.map(x=>({date:x.day.input.value,windows:windows(x)}));
  if(new Set(exceptions.map(d=>d.date)).size!==exceptions.length)throw Error('Há datas especiais repetidas. Mantenha uma configuração por data.');
  for(const d of exceptions)validatePeriods(d.windows);
  await api('/team/hours?employeeId='+employee,{method:'PUT',body:{version:data.version,weekly:rows.flatMap(x=>windows(x).map(window=>({day:Number(x.day.input.value),window}))),exceptions}});
  const saved=await api('/team/hours?employeeId='+employee);
  if(exceptions.some(d=>JSON.stringify(saved.exceptions.find(r=>r.date===d.date)?.windows)!==JSON.stringify(d.windows)))throw Error('A conferência encontrou diferenças. Reabra a disponibilidade para conferir os dados gravados.');
  toast('Datas e horários salvos para este profissional.');await workspace();
 },'primary'));
}
async function employees(host){await invitationsPanel(host);const people=await api('/owner/employees');host.append(button('Adicionar conta existente',()=>{const username=field('Usuário ou e-mail');modal('Adicionar profissional',form([username.root],'Adicionar à equipe',async()=>{await api('/owner/memberships',{method:'POST',body:{username:username.input.value}});dialog.close();await workspace('equipe');}));}));host.append(button('+ Funcionário',()=>{const name=field('Nome'),user=field('Usuário'),email=field('E-mail','email'),pw=field('Senha inicial','password');pw.input.minLength=10;modal('Adicionar funcionário',form([name.root,user.root,email.root,pw.root],'Criar acesso',async()=>{await api('/owner/employees',{method:'POST',body:{fullName:name.input.value,userName:user.input.value,email:email.input.value,password:pw.input.value}});dialog.close();await workspace();}));},'primary'));for(const person of people){const name=field('Nome público','text',person.displayName),active=el('input',{type:'checkbox',checked:person.enabled});host.append(el('div',{class:'card stack'},heading(person.userName),p(person.email,'caption'),name.root,el('label',{class:'check'},active,'Profissional ativo'),row(button('Salvar profissional',async()=>{await api('/owner/employees/'+person.id,{method:'PUT',body:{version:person.version,displayName:name.input.value,enabled:active.checked}});toast('Profissional atualizado.');await workspace();},'primary'),...(!business||business==='orbe'?[button('Redefinir senha',()=>{const pw=field('Nova senha','password');pw.input.minLength=10;modal('Redefinir acesso',form([pw.root],'Redefinir senha',async()=>{await api('/owner/employees/'+person.id+'/password',{method:'POST',body:{password:pw.input.value}});dialog.close();toast('Senha redefinida. As sessões anteriores foram encerradas.');}));})]:[]))));}}
async function services(host){
 const people=await api('/team/employees');if(!people.length){host.append(p('Cadastre um profissional na área Equipe para começar.'));return;}
 let employee=isOwner()?(people.some(x=>x.id===teamEmployee)?teamEmployee:people[0].id):people[0].id;
 const content=stack();if(isOwner()){const who=select('Profissional',people.map(x=>({id:x.id,name:x.displayName})),employee);who.input.onchange=()=>{employee=who.input.value;teamEmployee=employee;load().catch(e=>toast(e.message,true));};host.append(who.root);}host.append(content);
 async function load(){const selected=employee;const employeeQuery='?employeeId='+encodeURIComponent(selected);
 const [catalog,categories]=await Promise.all([api('/team/services'+employeeQuery),api('/team/categories'+employeeQuery)]);
 if(selected!==employee)return;content.replaceChildren();
 content.append(button('Gerenciar categorias',()=>manageCategories(selected,load)),button('+ Categoria',()=>{const name=field('Nome da categoria');modal('Nova categoria',form([name.root],'Salvar categoria',async()=>{await api('/team/categories'+employeeQuery,{method:'POST',body:{name:name.input.value}});dialog.close();await load();}));}));
 function edit(service=null){const name=field('Serviço','text',service?.name??''),duration=field('Duração · minutos','number',service?.durationMinutes??60),price=field('Preço · R$','text',((service?.priceCents??0)/100).toFixed(2)),enabled=el('input',{type:'checkbox',checked:service?.enabled??true});duration.input.min=1;duration.input.max=1440;
 const priceMode=select('Tipo de preço',[{id:'fixed',name:'Preço fixo'},{id:'from',name:'A partir de'}],service?.priceFrom?'from':'fixed');
 const category=select('Categoria',[{id:'',name:'Sem categoria'},...categories],service?.categoryId??'');
 modal(service?'Editar serviço':'Novo serviço',form([name.root,category.root,duration.root,priceMode.root,price.root,el('label',{class:'check'},enabled,'Disponível para reservar'),p('Alterações valem para este profissional. Reservas anteriores são preservadas.','caption')],'Salvar serviço',async()=>{await api('/team/services'+(service?'/'+service.id:'')+'?employeeId='+encodeURIComponent(selected),{method:service?'PUT':'POST',body:{name:name.input.value,durationMinutes:Number(duration.input.value),priceCents:cents(price.input.value),priceFrom:priceMode.input.value==='from',enabled:enabled.checked,categoryId:category.input.value||null,version:service?.version??null}});dialog.close();await load();}));}
 content.append(button('+ Serviço',()=>edit(),'primary'));if(!catalog.length)content.append(el('div',{class:'empty'},'Crie seu primeiro serviço para aparecer no catálogo deste profissional.'));
 for(const service of catalog)content.append(el('article',{class:'card between'},stack(heading(service.name),p(`${service.durationMinutes} min · ${servicePrice(service.priceCents,service.priceFrom)} · ${service.enabled?'Ativo':'Indisponível'}`,'caption')),row(button('Editar',()=>edit(service),'small'),button('Excluir',()=>modal('Excluir serviço?',stack(p('O serviço sairá deste profissional. Agendamentos e valores anteriores permanecem.'),button('Confirmar exclusão',async()=>{await api('/team/services/'+service.id+'?employeeId='+encodeURIComponent(selected),{method:'DELETE',body:{version:service.version}});dialog.close();await load();},'danger'))),'danger small'))));
 }await load();
}
async function settings(host){if(!business||business==='orbe')await platformInvitations(host);if(!business||business==='orbe'){await connectionSettings(host);host.append(button('+ Empresa',()=>{const name=field('Nome da empresa'),slug=field('Endereço da empresa'),owner=field('Usuário ou e-mail do administrador');modal('Cadastrar empresa',form([name.root,slug.root,owner.root],'Cadastrar empresa',async()=>{const company=await api('/platform/businesses',{method:'POST',body:{name:name.input.value,slug:slug.input.value,ownerUsername:owner.input.value}});dialog.close();toast('Empresa cadastrada em /'+company.slug);}));}));}const data=await api('/owner/settings'),name=field('Nome do estabelecimento','text',data.name),step=field('Intervalo entre opções · minutos','number',data.slotMinutes),lead=field('Antecedência mínima · minutos','number',data.leadMinutes),horizon=field('Disponibilidade · dias','number',data.horizonDays),cancel=field('Antecedência para remarcar · horas','number',data.cancellationHours),imported=el('input',{type:'checkbox',checked:data.importCompleted}),open=el('input',{type:'checkbox',checked:data.publicBookingOpen});host.append(el('div',{class:'card'},form([name.root,el('div',{class:'grid2'},step.root,lead.root,horizon.root,cancel.root),p('Fuso do estabelecimento: '+data.timeZoneId),el('label',{class:'check'},imported,'Conferi a importação dos dados antigos (ou não há dados para importar).'),el('label',{class:'check'},open,'Abrir reservas públicas'),p('Só abra após configurar serviços, profissionais, jornadas e atribuir os agendamentos importados.','caption')],'Salvar configurações',async()=>{await api('/owner/settings',{method:'PUT',body:{...data,name:name.input.value,slotMinutes:Number(step.input.value),leadMinutes:Number(lead.input.value),horizonDays:Number(horizon.input.value),cancellationHours:Number(cancel.input.value),importCompleted:imported.checked,publicBookingOpen:open.checked}});info=await api('/public/info');toast('Configurações salvas.');await workspace();})));}
async function imports(host){host.append(heading('Trazer meus dados do Nexus'),p('Use o exportador local do Orbe para criar uma cópia de segurança e um arquivo de importação. O banco original não será alterado.'),p('Após importar, atribua os agendamentos aos profissionais. Clientes antigos por nome não são transformados em contas.','caption'));const file=field('Arquivo de importação · JSON','file');file.input.accept='.json';host.append(file.root,button('Conferir arquivo',async()=>{const selected=file.input.files[0];if(!selected)throw Error('Selecione o arquivo exportado.');if(selected.size>2*1024*1024)throw Error('O arquivo deve ter até 2 MB. Entre em contato para importar um histórico maior.');const bundle=JSON.parse(await selected.text());const preview=await api('/owner/import/preview',{method:'POST',body:bundle});modal('Conferir importação',stack(p(`${preview.appointments} agendamentos · ${preview.entries} lançamentos`),p('Receitas: '+money(preview.income)+' · Despesas: '+money(preview.expense)),p(preview.alreadyImported?'Este arquivo já foi importado.':'Os registros entram sem funcionário, para sua conferência.'),button('Importar dados conferidos',async()=>{await api('/owner/import/commit',{method:'POST',body:bundle});dialog.close();toast('Dados importados. Confira as atribuições abaixo.');await workspace();},'primary')));},'primary'));
 const pending=await api('/owner/import/unassigned');if(!pending.appointments.length&&!pending.entries.length)host.append(el('div',{class:'empty'},'Nenhum registro aguardando atribuição.'));const people=await api('/owner/employees');for(const a of pending.appointments){const who=select('Profissional',[{id:'',name:'Selecione'},...people.filter(p=>p.enabled).map(p=>({id:p.id,name:p.displayName}))]);host.append(el('div',{class:'line-item stack'},el('strong',{},a.title),p(stamp(a.start),'caption'),who.root,button('Atribuir agendamento',async()=>{if(!who.input.value)throw Error('Selecione o profissional.');await api('/owner/import/appointments/'+a.id+'/assign',{method:'POST',body:{employeeId:who.input.value,version:a.version}});await workspace();},'small')));}for(const e of pending.entries){const who=select('Profissional',[{id:'',name:'Selecione'},...people.filter(p=>p.enabled).map(p=>({id:p.id,name:p.displayName}))]);host.append(el('div',{class:'line-item stack'},el('strong',{},e.description+' · '+money(e.cents)),p('Avulso sem atribuição; somente você visualiza.','caption'),who.root,button('Atribuir lançamento',async()=>{if(!who.input.value)throw Error('Selecione o profissional.');await api('/owner/import/entries/'+e.id+'/assign',{method:'POST',body:{employeeId:who.input.value,version:e.version}});await workspace();},'small')));}}
async function route(){const connection=await api('/public/connection');const destination=new URL(connection.apiUrl);if(destination.origin!==location.origin&&location.hostname!=='agendaorbe.com.br'){if(destination.protocol!=='https:'||!destination.hostname.endsWith('.discloud.dev'))throw Error('Endereço central inválido.');location.replace(destination.origin+location.pathname+location.search);return;}info=await api('/public/info');try{me=await api('/auth/me');}catch{me=null;}renderNav();const query=new URLSearchParams(location.search);if(location.pathname==='/confirmar-email'){await api('/auth/confirm?'+query.toString(),{method:'POST',body:{}});history.replaceState({},'', '/');csrf='';if(me){me=await api('/auth/me');renderNav();toast('E-mail confirmado.');return account();}toast('E-mail confirmado. Você já pode entrar.');return login();}if(location.pathname==='/redefinir-senha'){const pw=field('Nova senha','password');pw.input.minLength=10;return setView(el('section',{class:'auth card stack'},heading('Defina sua nova senha'),form([pw.root],'Salvar nova senha',async()=>{await api('/auth/reset',{method:'POST',body:{userId:query.get('userId'),token:query.get('token'),password:pw.input.value}});history.replaceState({},'', '/');toast('Senha alterada. Entre novamente.');login();})));}if(location.pathname==='/cadastro-profissional')return register(true);if(location.pathname==='/entrar')return login();if(location.pathname==='/recuperar-senha')return recover();if(location.pathname==='/equipe')return workspace();if(pendingEmail())return account();const slug=location.pathname.split('/').filter(Boolean)[0];if(slug){business=slug;if(!me)return login(booking);await booking();}else if(!me)return login(businessDirectory);else await businessDirectory();}
route().catch(e=>{renderNav();setView(el('section',{class:'auth card stack'},el('span',{class:'pill'},'Estamos preparando o Orbe'),heading('Seu espaço está sendo organizado'),p(e.message),button('Tentar novamente',()=>route(),'primary')));});

async function connectionSettings(host){const shared=await api('/public/connection'),address=field('Endereço HTTPS da aplicação Orbe','url',shared.apiUrl);host.append(el('section',{class:'card stack'},heading('Conexão central · somente administrador'),p('Este endereço vale para todos os dispositivos. O destino precisa usar o mesmo banco Orbe; contas e dados não são migrados por esta opção.'),form([address.root,p('Mantenha o endereço inicial de descoberta acessível para os aplicativos receberem as mudanças.','caption')],'Salvar conexão para todos',async()=>{modal('Alterar conexão para todos?',stack(p('PCs, celulares e site receberão o novo endereço. Só confirme se a aplicação de destino já estiver pronta.'),button('Confirmar alteração',async()=>{const updated=await api('/owner/connection',{method:'PUT',body:{version:shared.version,apiUrl:address.input.value}});dialog.close();if(new URL(updated.apiUrl).origin!==location.origin){location.replace(updated.apiUrl+'/equipe');return;}toast('Conexão central salva.');await workspace('configurações');},'primary')));})));}
let liveRefreshing=false;
async function refreshVisibleSchedule(){if(liveRefreshing||document.visibilityState!=='visible'||!me||!main.dataset.live||dialog.open||main.querySelector('form'))return;const view=main.dataset.live;liveRefreshing=true;try{const connection=await api('/public/connection');if(new URL(connection.apiUrl).origin!==location.origin&&location.hostname!=='agendaorbe.com.br'){const target=new URL(connection.apiUrl);if(target.protocol==='https:'&&target.hostname.endsWith('.discloud.dev'))location.replace(target.origin+location.pathname);return;}const rows=await allPages(view==='mine'?'/bookings/mine'+(bookingFilter?'?business='+encodeURIComponent(bookingFilter):''):view==='finance'?'/team/entries?employeeId='+teamEmployee:'/team/appointments?employeeId='+teamEmployee,view==='mine'?100:200);if(main.dataset.live!==view||dialog.open||main.querySelector('form'))return;if(JSON.stringify(rows)!==main.dataset.signature){const y=window.scrollY;await(view==='mine'?mine():workspace(view==='finance'?'financeiro':'agenda'));window.scrollTo({top:y,behavior:'instant'});}}catch{if(!me)login();}finally{liveRefreshing=false;}}
setInterval(refreshVisibleSchedule,15000);document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible')refreshVisibleSchedule();});

setInterval(pollNotices,15000);document.addEventListener('visibilitychange',()=>{if(document.visibilityState==='visible')pollNotices();});setTimeout(pollNotices,1500);
async function membershipAccount(){
 const requests=await api('/memberships/requests/mine');
 const code=field('Código de convite da empresa');
 const status=stack(...requests.map(r=>p(r.name+' · '+({Pending:'Aguardando aprovação',Approved:'Aprovado',Rejected:'Rejeitado'}[r.status]??r.status))));
 return el('details',{},el('summary',{},'Acesso profissional'),status,form([code.root],'Solicitar ingresso',async()=>{
  toast((await api('/memberships/request',{method:'POST',body:{code:code.input.value}})).message);await account();
 }));
}
async function invitationsPanel(host){
 const [invitation,requests]=await Promise.all([api('/owner/invitation'),api('/owner/membership-requests')]);
 const panel=el('details',{class:'card'},el('summary',{},'Convites e solicitações da equipe'),p('Compartilhe o código. Você aprova cada ingresso.'),el('code',{},invitation.code),
  button('Copiar código',async()=>{await navigator.clipboard.writeText(invitation.code);toast('Código copiado.');},'small'));
 for(const r of requests){
  panel.append(el('article',{class:'line-item'},heading(r.fullName),p(r.email,'caption'),p(r.emailConfirmed?'E-mail confirmado':'Aguardando confirmação do e-mail','caption'),
   row(button('Aprovar',async()=>{await api('/owner/membership-requests/'+r.id,{method:'PUT',body:{approve:true}});await workspace('equipe');},'primary small'),
   button('Rejeitar',()=>modal('Rejeitar solicitação?',stack(p('O profissional poderá solicitar ingresso novamente.'),button('Confirmar rejeição',async()=>{await api('/owner/membership-requests/'+r.id,{method:'PUT',body:{approve:false}});dialog.close();await workspace('equipe');},'danger'))),'small'))));
 }
 if(!requests.length)panel.append(p('Nenhuma solicitação pendente.'));
 host.append(panel);
}
async function platformInvitations(host){
 const companies=await api('/platform/invitations');
 const panel=el('details',{class:'card'},el('summary',{},'Convites das empresas · administração Orbe'));
 for(const b of companies)panel.append(el('div',{class:'line-item'},heading(b.name),p('/'+b.slug,'caption'),el('code',{},b.code??'Código ainda não gerado'),
  button('Gerar novo código',()=>modal('Trocar código de convite?',stack(p('O código anterior deixará de aceitar novas solicitações. As solicitações recebidas continuarão aguardando análise.'),button('Confirmar troca',async()=>{
   await api('/platform/invitations/'+b.id+'/rotate',{method:'POST',body:{}});dialog.close();await workspace('configurações');
  },'primary'))),'small')));
 host.append(panel);
}
async function manageCategories(employee,refresh){
 const query='?employeeId='+encodeURIComponent(employee),categories=await api('/team/categories'+query);
 const list=stack();
 async function reload(){await refresh();await manageCategories(employee,refresh);}
 for(const c of categories){
  const name=field('Nome da categoria','text',c.name);
  list.append(el('div',{class:'line-item stack'},name.root,row(button('Salvar nome',async()=>{
   await api('/team/categories/'+c.id+query,{method:'PUT',body:{name:name.input.value,expectedName:c.name}});await reload();
  },'small'),button('Excluir',()=>modal('Excluir categoria?',stack(p('Os serviços vinculados ficarão sem categoria.'),button('Confirmar exclusão',async()=>{
   await api('/team/categories/'+c.id+query,{method:'DELETE',body:{name:c.name}});await reload();
  },'danger'))),'danger small'))));
 }
 if(!categories.length)list.append(p('Nenhuma categoria criada para este profissional.'));
 modal('Categorias do profissional',list);
}

function validatePeriods(periods){
 const sorted=[...periods].sort((a,b)=>a.startMinute-b.startMinute);
 for(let i=0;i<sorted.length;i++){
  const p=sorted[i];if(!Number.isInteger(p.startMinute)||!Number.isInteger(p.endMinute)||p.startMinute<0||p.endMinute>1440||p.endMinute<=p.startMinute||i&&p.startMinute<sorted[i-1].endMinute)throw Error('Confira os períodos. O fim deve vir após o início, sem sobreposição.');
 }
}
function dateSelection(){
 const mode=select('Selecionar datas',[{id:'single',name:'Um dia'},{id:'multiple',name:'Vários dias'},{id:'range',name:'Intervalo'}],'single');
 const day=field('Dia','date',today()),first=field('Data inicial','date',today()),last=field('Data final','date',today());
 const selected=new Set(),calendar=stack(),preview=p('','caption date-preview'),checks=[];
 let month=today().slice(0,7);
 const filters=row();
 for(let i=0;i<7;i++){const box=el('input',{type:'checkbox',checked:true});checks.push(box);box.onchange=updatePreview;filters.append(el('label',{class:'check'},box,weekdays[i].slice(0,3)));}
 const range=stack(first.root,last.root,p('Incluir estes dias da semana','caption'),filters);
 function dates(){
  if(mode.input.value==='single'){if(!day.input.value)throw Error('Escolha um dia.');return [day.input.value];}
  if(mode.input.value==='multiple'){if(!selected.size||selected.size>366)throw Error('Selecione de 1 a 366 datas.');return [...selected].sort();}
  const start=first.input.value,end=last.input.value;
  const count=Math.round((new Date(end+'T12:00:00Z')-new Date(start+'T12:00:00Z'))/86400000)+1;
  if(!Number.isInteger(count)||count<1||count>366)throw Error('Escolha um intervalo de até 366 dias com a data final após a inicial.');
  const result=[];
  for(let i=0;i<count;i++){const d=plus(start,i);if(checks[new Date(d+'T12:00:00Z').getUTCDay()].checked)result.push(d);}
  if(!result.length)throw Error('Marque ao menos um dia da semana.');
  return result;
 }
 function updatePreview(){
  try{const values=dates();preview.textContent=values.length+' data(s): '+values.map(d=>d.split('-').reverse().join('/')).join(', ');}
  catch(e){preview.textContent=e.message;}
 }
 function renderMonth(){
  calendar.replaceChildren();const date=new Date(month+'-01T12:00:00Z'),grid=el('div',{class:'days'});
  function move(delta){date.setUTCMonth(date.getUTCMonth()+delta);if(date.getUTCFullYear()<1900||date.getUTCFullYear()>2100)return;month=date.toISOString().slice(0,7);renderMonth();}
  calendar.append(el('div',{class:'between'},el('strong',{},new Intl.DateTimeFormat('pt-BR',{month:'long',year:'numeric',timeZone:'UTC'}).format(date)),row(button('‹',()=>move(-1),'small'),button('›',()=>move(1),'small'))),grid);
  for(const name of ['DOM','SEG','TER','QUA','QUI','SEX','SÁB'])grid.append(el('span',{class:'weekday'},name));
  for(let i=0;i<date.getUTCDay();i++)grid.append(el('span',{'aria-hidden':'true'}));
  const count=new Date(Date.UTC(date.getUTCFullYear(),date.getUTCMonth()+1,0)).getUTCDate();
  for(let i=1;i<=count;i++){
   const value=month+'-'+String(i).padStart(2,'0');
   const b=button(String(i),()=>{if(!selected.delete(value))selected.add(value);renderMonth();updatePreview();},'day'+(selected.has(value)?' selected':''));
   b.setAttribute('aria-label',value);b.setAttribute('aria-pressed',String(selected.has(value)));grid.append(b);
  }
 }
 function updateMode(){day.input.disabled=mode.input.value!=='single';first.input.disabled=last.input.disabled=mode.input.value!=='range';day.root.hidden=mode.input.value!=='single';calendar.hidden=mode.input.value!=='multiple';range.hidden=mode.input.value!=='range';renderMonth();updatePreview();}
 mode.input.onchange=updateMode;day.input.onchange=first.input.onchange=last.input.onchange=updatePreview;
 const root=stack(mode.root,day.root,calendar,range,preview);updateMode();return {root,dates};
}
