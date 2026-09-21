using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    /// <summary>The only stock mutation boundary. Every stack belongs to exactly one inventory.</summary>
    public sealed class ColonyInventory
    {
        readonly ColonyState state;readonly ColonyCatalog catalog;
        readonly Dictionary<string,InventoryState> index=new Dictionary<string,InventoryState>();
        readonly Dictionary<string,List<ReservationState>> claims=new Dictionary<string,List<ReservationState>>();
        public long WaterRevision{get;private set;}
        public ColonyInventory(ColonyState data,ColonyCatalog definitions){state=data;catalog=definitions;Reindex();}
        public void Reindex(){index.Clear();claims.Clear();foreach(var inventory in state.inventories)index.Add(inventory.id,inventory);foreach(var claim in state.reservations)IndexClaim(claim);}
        void IndexClaim(ReservationState claim){if(!claims.TryGetValue(claim.inventory,out var list)){list=new List<ReservationState>();claims.Add(claim.inventory,list);}list.Add(claim);}
        IEnumerable<ReservationState> Claims(InventoryState inventory)=>inventory!=null&&claims.TryGetValue(inventory.id,out var list)?(IEnumerable<ReservationState>)list:Array.Empty<ReservationState>();
        public InventoryState Get(string id)=>id!=null&&index.TryGetValue(id,out var inventory)?inventory:null;
        public InventoryState Create(string owner,float capacity,int slots=1000,bool waterOnly=false)
        {var inventory=new InventoryState{id=state.Id("inventory"),owner=owner,capacity=capacity,slots=slots,waterOnly=waterOnly};state.inventories.Add(inventory);index.Add(inventory.id,inventory);return inventory;}
        public float Used(InventoryState inventory){float sum=0;if(inventory!=null)foreach(var item in inventory.items)sum+=item.quantity;return sum;}
        public float Count(InventoryState inventory,Good good){float sum=0;if(inventory!=null)foreach(var item in inventory.items)if(item.good==good)sum+=item.quantity;return sum;}
        public float Reserved(InventoryState inventory,Good good,string exceptJob=null){float sum=0;foreach(var r in Claims(inventory))if(!r.incoming&&r.good==good&&r.job!=exceptJob)sum+=r.quantity;return sum;}
        public float Incoming(InventoryState inventory,Good good){float sum=0;foreach(var r in Claims(inventory))if(r.incoming&&r.good==good)sum+=r.quantity;return sum;}
        public float Available(InventoryState inventory,Good good,string job=null)=>Mathf.Max(0,Count(inventory,good)-Reserved(inventory,good,job));
        public bool Accepts(InventoryState inventory,Good good)=>inventory!=null&&(!inventory.waterOnly||good==Good.Water)&&(inventory.filters.Count==0||inventory.filters.Contains(good));
        public float Free(InventoryState inventory,Good good,string job=null)
        {
            if(!Accepts(inventory,good))return 0;
            float incoming=0;foreach(var r in Claims(inventory))if(r.incoming&&r.job!=job)incoming+=r.quantity;
            float stackLimit=catalog.Good(good).stack;
            float slots=Mathf.Max(0,inventory.slots-inventory.items.Count)*stackLimit+inventory.items.Where(i=>i.good==good&&i.condition>=.999f).Sum(i=>Mathf.Max(0,stackLimit-i.quantity));
            return Mathf.Max(0,Mathf.Min(inventory.capacity-Used(inventory)-incoming,slots));
        }
        public float Add(InventoryState inventory,Good good,float quantity,float condition=1)
        {
            if(inventory.waterOnly&&quantity>0)WaterRevision++;
            float accepted=Mathf.Min(Mathf.Max(0,quantity),Free(inventory,good)),remaining=accepted,limit=catalog.Good(good).stack;
            foreach(var item in inventory.items){if(item.good!=good||Mathf.Abs(item.condition-condition)>.001f)continue;float amount=Mathf.Min(remaining,limit-item.quantity);item.quantity+=amount;remaining-=amount;if(remaining<=.0001f)return accepted;}
            while(remaining>.0001f&&inventory.items.Count<inventory.slots){float amount=Mathf.Min(remaining,limit);inventory.items.Add(new ItemStack{id=state.Id("item"),good=good,quantity=amount,condition=condition});remaining-=amount;}
            return accepted-remaining;
        }
        public bool Consume(InventoryState inventory,Good good,float quantity,string job=null)
        {
            if(quantity<0||Available(inventory,good,job)+.0001f<quantity)return false;
            if(inventory.waterOnly&&quantity>0)WaterRevision++;
            float remaining=quantity;
            for(int i=inventory.items.Count-1;i>=0&&remaining>.0001f;i--){var item=inventory.items[i];if(item.good!=good)continue;float take=Mathf.Min(item.quantity,remaining);item.quantity-=take;remaining-=take;if(item.quantity<=.0001f)inventory.items.RemoveAt(i);}
            return true;
        }
        public bool CanConsume(InventoryState inventory,IEnumerable<Amount> amounts,string job=null)=>amounts.All(a=>Available(inventory,a.good,job)+.0001f>=a.quantity);
        public bool Consume(InventoryState inventory,IEnumerable<Amount> amounts,string job=null){var list=amounts.ToArray();if(!CanConsume(inventory,list,job))return false;foreach(var amount in list)Consume(inventory,amount.good,amount.quantity,job);return true;}
        public float Transfer(InventoryState source,InventoryState destination,Good good,float requested,string job=null)
        {
            if(source==null||destination==null||source==destination)return 0;
            if((source.waterOnly||destination.waterOnly)&&requested>0)WaterRevision++;
            float quantity=Mathf.Min(Mathf.Max(0,requested),Available(source,good,job),Free(destination,good,job)),left=quantity;
            // Whole stacks retain identity; a split creates a new identity only for the separated quantity.
            for(int i=source.items.Count-1;i>=0&&left>.0001f;i--){var item=source.items[i];if(item.good!=good)continue;float amount=Mathf.Min(left,item.quantity);
                if(amount>=item.quantity-.0001f && destination.items.Count<destination.slots){source.items.RemoveAt(i);destination.items.Add(item);left-=amount;}
                else{
                    float placed=0,limit=catalog.Good(good).stack;
                    foreach(var target in destination.items){if(target.good!=good||Mathf.Abs(target.condition-item.condition)>.001f)continue;float move=Mathf.Min(amount-placed,limit-target.quantity);target.quantity+=move;placed+=move;if(placed>=amount-.0001f)break;}
                    if(placed<amount && destination.items.Count<destination.slots){destination.items.Add(new ItemStack{id=state.Id("item"),good=good,quantity=amount-placed,condition=item.condition});placed=amount;}
                    item.quantity-=placed;left-=placed;if(item.quantity<=.0001f)source.items.RemoveAt(i);
                }
            }
            return quantity-left;
        }
        public bool Reserve(string job,InventoryState source,InventoryState destination,Good good,float amount)
        {
            if(amount<=0||Available(source,good)<amount-.0001f||Free(destination,good)<amount-.0001f)return false;
            if(source.waterOnly||destination.waterOnly)WaterRevision++;
            var outgoing=new ReservationState{id=state.Id("claim"),job=job,inventory=source.id,good=good,quantity=amount};state.reservations.Add(outgoing);IndexClaim(outgoing);
            var incoming=new ReservationState{id=state.Id("claim"),job=job,inventory=destination.id,good=good,quantity=amount,incoming=true};state.reservations.Add(incoming);IndexClaim(incoming);return true;
        }
        public void Release(string job,bool outgoingOnly=false)
        {for(int i=state.reservations.Count-1;i>=0;i--){var r=state.reservations[i];if(r.job!=job||outgoingOnly&&r.incoming)continue;if(Get(r.inventory)?.waterOnly==true)WaterRevision++;state.reservations.RemoveAt(i);if(claims.TryGetValue(r.inventory,out var list))list.Remove(r);}}
        public void Validate()
        {
            var items=new HashSet<string>();foreach(var inventory in state.inventories){if(Used(inventory)>inventory.capacity+.02f||inventory.items.Count>inventory.slots)throw new InvalidOperationException("Inventory exceeds capacity: "+inventory.id);foreach(var item in inventory.items)if(item.quantity<=0||!items.Add(item.id))throw new InvalidOperationException("Invalid or multiply owned item: "+item.id);}
            foreach(var group in state.reservations.Where(r=>!r.incoming).GroupBy(r=>new{r.inventory,r.good}))if(Get(group.Key.inventory)==null||group.Sum(r=>r.quantity)>Count(Get(group.Key.inventory),group.Key.good)+.01f)throw new InvalidOperationException("Unbacked material reservation");
            foreach(var group in state.reservations.Where(r=>r.incoming).GroupBy(r=>r.inventory)){var inventory=Get(group.Key);if(inventory==null||Used(inventory)+group.Sum(r=>r.quantity)>inventory.capacity+.02f)throw new InvalidOperationException("Incoming cargo exceeds reserved storage capacity");}
        }
    }
}
