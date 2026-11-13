using System.Net.Mail;
using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

public sealed record SupportContacts(string? WhatsApp, string? Email);

public static class SupportRoutes
{
 public static void MapSupport(this WebApplication app)
 {
  var support=app.MapGroup("/api/v1/support").RequireAuthorization();
  support.MapGet("",async(OrbeDb db)=>await db.Settings.Select(x=>new SupportContacts(x.SupportWhatsApp,x.SupportEmail)).SingleAsync());
  support.MapPut("",async(SupportContacts input,OrbeDb db)=>{
   var phone=input.WhatsApp?.Trim();
   var email=input.Email?.Trim();
   if(!string.IsNullOrEmpty(phone)&&(phone.Length is <10 or >15||phone[0]=='0'||phone.Any(c=>!char.IsAsciiDigit(c))))
    throw new ApiError(400,"Informe o WhatsApp com código do país e DDD, usando apenas números.");
   if(!string.IsNullOrEmpty(email)&&(email.Length>254||!MailAddress.TryCreate(email,out var address)||address.Address!=email))
    throw new ApiError(400,"Informe um endereço de e-mail válido.");
   var settings=await db.Settings.SingleAsync();
   settings.SupportWhatsApp=string.IsNullOrEmpty(phone)?null:phone;
   settings.SupportEmail=string.IsNullOrEmpty(email)?null:email;
   await db.SaveChangesAsync();
   return new SupportContacts(settings.SupportWhatsApp,settings.SupportEmail);
  }).RequireAuthorization(p=>p.RequireRole(Roles.Owner));
 }
}
