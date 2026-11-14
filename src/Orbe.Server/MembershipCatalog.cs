using Microsoft.EntityFrameworkCore;

namespace Orbe.Server;

internal static class MembershipCatalog
{
 internal static async Task Copy(OrbeDb db,Employee target,int business)
 {
  var links=db.EmployeeServices.IgnoreQueryFilters();
  if(await links.AnyAsync(x=>x.EmployeeId==target.Id))return;
  var candidates=from employee in db.Employees.IgnoreQueryFilters()
                 where employee.UserId==target.UserId&&employee.Id!=target.Id&&employee.Enabled
                       &&links.Any(x=>x.EmployeeId==employee.Id)
                 orderby employee.BusinessId,employee.Id
                 select employee.Id;
  var source=await candidates.FirstOrDefaultAsync();
  if(source is null)return;
  var catalog=await (from link in links
    join service in db.Services.IgnoreQueryFilters() on link.ServiceId equals service.Id
    where link.EmployeeId==source
    select new{ Name=link.Name??service.Name, Duration=link.DurationMinutes??service.DurationMinutes,
     Price=link.PriceCents??service.PriceCents, From=link.PriceFrom??service.PriceFrom,
     Enabled=link.Enabled??service.Enabled, Category=db.Categories.IgnoreQueryFilters().Where(c=>c.Id==link.CategoryId).Select(c=>c.Name).FirstOrDefault() }).ToArrayAsync();
  var categories=await db.Categories.Where(x=>x.BusinessId==business).ToDictionaryAsync(x=>x.NormalizedName);
  foreach(var offer in catalog)
  {
   string? categoryId=null;
   if(offer.Category is {} name)
   {
    var key=name.ToUpperInvariant();
    if(!categories.TryGetValue(key,out var category))
    {
     category=new(){Name=name,NormalizedName=key};
     db.Categories.Add(category);
     categories.Add(key,category);
    }
    categoryId=category.Id;
   }
   var item=new Service{Name=offer.Name,DurationMinutes=offer.Duration,PriceCents=offer.Price,PriceFrom=offer.From,Enabled=offer.Enabled};
   db.Services.Add(item);
   db.EmployeeServices.Add(new(){EmployeeId=target.Id,ServiceId=item.Id,CategoryId=categoryId});
  }
 }
}
