using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed class ColonyTraffic
    {
        readonly ColonySimulation sim;
        public ColonyTraffic(ColonySimulation owner){sim=owner;}
        public bool Active(FlightState f)=>f.phase!=FlightPhase.Complete&&f.phase!=FlightPhase.Cancelled;
        public StructureState Pad()=>sim.State.structures.Where(b=>(b.definition=="apron"||b.definition=="spaceport")&&b.phase==BuildPhase.Complete&&b.enabled&&b.condition>.2f).OrderByDescending(b=>b.definition=="spaceport").FirstOrDefault();
        public string PersonnelWarning(IEnumerable<string> people)
        {
            var ids=people.Distinct().ToArray();if(ids.Length<1||ids.Length>sim.Catalog.balance.flightSeats)return "Select one to six sleeping colonists";
            if(ids.Any(id=>sim.Actor(id)==null||sim.Actor(id).location!=PersonLocation.Sleeping||sim.Actor(id).kind!=ActorKind.Colonist))return "A selected passenger is no longer available";
            if(sim.People.FreeBeds(false).Count<ids.Length)return "Build, connect and supply enough unreserved habitat beds";
            var pad=Pad();if(!sim.Networks.SafeLanding(pad,out var why))return why;
            if(sim.State.credits<sim.Catalog.balance.flightFee)return "Insufficient credits for transport";return null;
        }
        FlightState New(FlightKind kind,string name)
        {
            var f=new FlightState{id=sim.State.Id("flight"),kind=kind,name=name,pad=Pad()?.id,duration=sim.Day*(kind==FlightKind.Import||kind==FlightKind.Export?sim.Catalog.balance.freightDays:sim.Catalog.balance.flightDays)};
            f.inventory=sim.Stock.Create(f.id,sim.Catalog.balance.freightCapacity).id;f.position=sim.Position(f.pad)+Vector3.up*300;sim.State.flights.Add(f);return f;
        }
        public FlightState RequestPersonnel(IEnumerable<string> people)
        {
            var list=people.Distinct().ToArray();string error=PersonnelWarning(list);if(error!=null)throw new InvalidOperationException(error);
            var f=New(FlightKind.Personnel,"Personnel transport");f.fee=sim.Catalog.balance.flightFee;f.passengers=list.ToList();var beds=sim.People.FreeBeds(false);
            for(int i=0;i<list.Length;i++){var a=sim.Actor(list[i]);a.location=PersonLocation.Reserved;a.home=beds[i];a.activity="Reserved on "+f.name;f.beds.Add(beds[i]);}
            sim.Credit(-f.fee,"Transport","Personnel flight · "+list.Length+" passengers",f.id+":fee");return f;
        }
        public float Quote(Good good,float quantity,bool buy)=>sim.Catalog.Good(good).buy*quantity*(buy?1:.6f);
        public FlightState Trade(Good good,float quantity,bool buy)
        {
            quantity=Mathf.Floor(quantity);if(quantity<1||quantity>sim.Catalog.balance.freightCapacity)throw new InvalidOperationException("Freight manifest must contain 1–200 cargo units");
            if(Pad()==null)throw new InvalidOperationException("An intact landing apron is required");
            float price=Quote(good,quantity,buy);if(buy&&sim.State.credits<price)throw new InvalidOperationException("Insufficient credits");
            if(!buy)
            {
                var candidates=sim.State.structures.Where(b=>b.phase==BuildPhase.Complete).SelectMany(b=>new[]{sim.Stock.Get(b.inventory),sim.Stock.Get(b.waterInventory)}).Where(i=>i!=null);
                if(candidates.Sum(i=>sim.Stock.Available(i,good))<quantity)throw new InvalidOperationException("Not enough unreserved goods to export");
            }
            var f=New(buy?FlightKind.Import:FlightKind.Export,(buy?"Import · ":"Export · ")+good);f.cargo.Add(new Amount(good,quantity));f.unitPrice=sim.Catalog.Good(good).buy*(buy?1:.6f);f.fee=price;
            if(buy)sim.Credit(-price,"Imports",f.name+" × "+quantity,f.id+":purchase");
            else
            {
                var staging=new StructureState{id=f.id,inventory=f.inventory};sim.Jobs.Supply(staging,good,quantity,4);
                float reserved=sim.Stock.Incoming(sim.Stock.Get(f.inventory),good);
                if(reserved+1e-3f<quantity){Cancel(f);throw new InvalidOperationException("Available stock is held for local production; move export goods into storage");}
            }
            return f;
        }
        public RecruitmentState Recruit(Profession profession)
        {
            float price=sim.Catalog.balance.recruitCost;if(sim.State.credits<price)throw new InvalidOperationException("Insufficient recruitment credits");
            var order=new RecruitmentState{id=sim.State.Id("recruitment"),profession=profession,remaining=sim.Day*2};sim.Credit(-price,"Recruitment",profession+" recruited to orbital ship",order.id);sim.State.recruitment.Add(order);return order;
        }
        void ReleasePassengers(FlightState f)
        {
            foreach(var id in f.passengers){var a=sim.Actor(id);if(a==null||a.location==PersonLocation.Arrived||a.location==PersonLocation.Dead)continue;a.home=null;a.location=a.kind==ActorKind.Visitor?PersonLocation.Departed:PersonLocation.Sleeping;a.activity="Orbital cryosleep";}f.beds.Clear();
        }
        public void Cancel(FlightState f)
        {
            if(!Active(f))return;if(f.delivered)throw new InvalidOperationException("Manifest already delivered; departure is under way");
            if(f.phase!=FlightPhase.Queued&&f.phase!=FlightPhase.Preparing){f.returnRequested=true;f.blocker="Return requested; preserving manifest";return;}
            foreach(var j in sim.State.jobs.Where(j=>sim.Jobs.Active(j)&&j.target==f.id).ToArray())sim.Jobs.Cancel(j,"Flight cancelled; carried goods retained");
            RecoverFreight(f);ReleasePassengers(f);f.phase=FlightPhase.Cancelled;
            if(!f.refunded&&(f.kind==FlightKind.Personnel||f.kind==FlightKind.Import)){sim.Credit(f.fee,"Refunds","Cancelled "+f.name,f.id+":refund");f.refunded=true;}
        }
        void RecoverFreight(FlightState f)
        {
            var cargo=sim.Stock.Get(f.inventory);if(sim.Stock.Used(cargo)<=0)return;var pile=sim.DropCargo(sim.Position(f.pad),Array.Empty<Amount>(),"Flight cargo · "+f.name);
            foreach(var item in cargo.items.ToArray())sim.Stock.Transfer(cargo,sim.Stock.Get(pile.inventory),item.good,item.quantity);
        }
        void Advance(FlightState f,FlightPhase phase){f.phase=phase;f.elapsed=0;f.blocker=phase.ToString();}
        public void Tick(float dt)
        {
            foreach(var recruit in sim.State.recruitment.Where(r=>!r.completed)){recruit.remaining-=dt;if(recruit.remaining<=0){sim.AddPerson(recruit.profession,false);recruit.completed=true;}}
            Departures();var active=sim.State.flights.FirstOrDefault(f=>Active(f)&&f.phase!=FlightPhase.Queued);
            if(active==null){active=sim.State.flights.FirstOrDefault(Active);if(active!=null){Advance(active,FlightPhase.Preparing);foreach(string id in active.passengers){var a=sim.Actor(id);if(a!=null&&active.kind!=FlightKind.Departure)a.location=PersonLocation.Waking;}}}
            if(active==null)return;var f=active;f.elapsed+=dt;var pad=sim.Structure(f.pad);
            if(pad==null||pad.phase==BuildPhase.Removed){pad=Pad();if(pad!=null)f.pad=pad.id;}
            Vector3 ground=pad?.position??sim.State.setup.landing.Position;
            bool passenger=f.kind==FlightKind.Personnel||f.kind==FlightKind.Visitors;
            if(f.returnRequested&&f.phase!=FlightPhase.Returning&&f.phase!=FlightPhase.Preparing&&f.phase!=FlightPhase.Queued&&f.phase!=FlightPhase.Complete&&!f.delivered)Advance(f,FlightPhase.Returning);
            switch(f.phase)
            {
                case FlightPhase.Preparing:
                    if(pad==null){f.blocker="Awaiting intact landing apron";break;}
                    if(f.kind==FlightKind.Export)
                    {
                        f.position=ground;f.blocker="Loading reserved export cargo";if(f.cargo.Any(c=>sim.Stock.Count(sim.Stock.Get(f.inventory),c.good)+.001f<c.quantity))break;
                        if(f.elapsed<6)break;Advance(f,FlightPhase.Departing);break;
                    }
                    if(f.kind==FlightKind.Departure)
                    {
                        f.position=ground;bool all=true;
                        foreach(var id in f.passengers){var a=sim.Actor(id);if(a==null||a.location==PersonLocation.Dead)continue;if(a.location==PersonLocation.InTransit)continue;
                            if(a.intention!="depart"||a.target!=pad.id){sim.Navigation.Stop(a);a.intention="depart";a.target=pad.id;}
                            a.activity="Boarding departure shuttle";if(Vector3.Distance(a.position,ground)>3){if(sim.Navigation.Arrived(a))sim.Navigation.Go(a,ground,pad.id);all=false;}else{a.location=PersonLocation.InTransit;sim.Navigation.Stop(a);a.activity="Boarded departure shuttle";}}
                        f.blocker=all?"Passengers aboard":"Boarding through sealed gangway";if(all&&f.elapsed>6)Advance(f,FlightPhase.Departing);break;
                    }
                    if(passenger&&!sim.Networks.SafeLanding(pad,out var warning)){f.blocker=warning;break;}
                    if(f.elapsed>=10){foreach(var id in f.passengers){var a=sim.Actor(id);if(a!=null)a.location=PersonLocation.InTransit;}Advance(f,FlightPhase.Transit);}break;
                case FlightPhase.Transit:
                    f.position=ground+Vector3.up*300;if(f.elapsed>=Mathf.Max(1,f.duration-10))Advance(f,FlightPhase.Holding);break;
                case FlightPhase.Holding:
                    if(pad==null){f.blocker="Holding: no landing apron";break;}
                    if(passenger&&!SafeManifest(f,pad,out var hold)){f.blocker="Holding: "+hold;break;}
                    if(f.kind==FlightKind.Import&&!f.committed){foreach(var item in f.cargo)sim.Stock.Add(sim.Stock.Get(f.inventory),item.good,item.quantity);f.committed=true;}
                    Advance(f,FlightPhase.Descending);break;
                case FlightPhase.Descending:
                    f.position=ground+Vector3.up*Mathf.Lerp(180,0,Mathf.SmoothStep(0,1,f.elapsed/8));if(f.elapsed>=8)Advance(f,FlightPhase.Disembarking);break;
                case FlightPhase.Disembarking:
                    f.position=ground;
                    if(passenger)
                    {
                        if(!SafeManifest(f,pad,out var safety)){f.blocker="Gangway sealed: "+safety;break;}
                        int allowed=Mathf.FloorToInt(f.elapsed/.8f);for(int i=0;i<Mathf.Min(allowed,f.passengers.Count);i++)
                        {
                            var a=sim.Actor(f.passengers[i]);if(a==null||a.location==PersonLocation.Arrived||a.location==PersonLocation.Dead)continue;
                            a.location=PersonLocation.Arrived;a.position=ground+Vector3.right*(i%2);a.building=pad.id;a.activity="Entering sealed colony";a.intention=null;a.returnAfter=(float)sim.State.time+(2+sim.State.Random()*2)*sim.Day;
                            a.history.Add("Arrived on colony day "+((int)(sim.State.time/sim.Day)+1));var home=sim.Structure(a.home);if(home!=null)sim.Navigation.Go(a,home.position,home.id);sim.Notify(a.id);
                        }
                    }
                    else if(f.kind==FlightKind.Import&&sim.Stock.Used(sim.Stock.Get(f.inventory))>0)RecoverFreight(f);
                    if(f.kind==FlightKind.Import||passenger&&f.passengers.All(id=>sim.Actor(id)?.location==PersonLocation.Arrived))f.delivered=true;
                    if(f.elapsed>=Mathf.Max(5,f.passengers.Count*.8f+2))Advance(f,FlightPhase.Departing);break;
                case FlightPhase.Departing:
                    f.position=ground+Vector3.up*Mathf.Lerp(0,200,Mathf.SmoothStep(0,1,f.elapsed/7));
                    if(f.elapsed<7)break;
                    if(f.kind==FlightKind.Export&&!f.committed){foreach(var amount in f.cargo)if(!sim.Stock.Consume(sim.Stock.Get(f.inventory),amount.good,amount.quantity))throw new InvalidOperationException("Export manifest is not physically loaded");sim.Credit(f.fee,"Exports",f.name,f.id+":sale");f.committed=true;}
                    if(f.kind==FlightKind.Departure)foreach(string id in f.passengers){var a=sim.Actor(id);if(a==null||a.location==PersonLocation.Dead)continue;a.location=PersonLocation.Departed;a.home=null;a.activity="Returned to galactic network";}
                    Advance(f,FlightPhase.Complete);break;
                case FlightPhase.Returning:
                    f.position=ground+Vector3.up*(30+f.elapsed*20);if(f.elapsed>=8){if(f.kind==FlightKind.Import){var returning=sim.Stock.Get(f.inventory);foreach(var item in returning.items.ToArray())sim.Stock.Consume(returning,item.good,item.quantity);}else RecoverFreight(f);ReleasePassengers(f);if(f.kind==FlightKind.Import&&!f.refunded){sim.Credit(f.fee,"Refunds","Returned purchase",f.id+":refund");f.refunded=true;}Advance(f,FlightPhase.Cancelled);}break;
            }
        }
        bool SafeManifest(FlightState f,StructureState pad,out string reason)
        {
            if(!sim.Networks.SafeLanding(pad,out reason))return false;
            for(int i=0;i<f.passengers.Count;i++)
            {
                var a=sim.Actor(f.passengers[i]);if(a==null||a.location==PersonLocation.Dead||a.location==PersonLocation.Arrived)continue;
                var bed=sim.Structure(a.home);if(bed!=null&&sim.Operating(bed)&&sim.Networks.Connected(pad.id,bed.id))continue;
                var replacement=sim.People.FreeBeds(a.kind==ActorKind.Visitor).FirstOrDefault(id=>sim.Networks.Connected(pad.id,id));
                if(replacement==null){reason="Reserved room has lost safe access; add connected accommodation or return the flight";return false;}
                a.home=replacement;if(i<f.beds.Count)f.beds[i]=replacement;else f.beds.Add(replacement);
            }
            reason="Ready";return true;
        }
        public bool AdmitVisitors()
        {
            if(!sim.State.visitorsOpen||!sim.State.completedResearch.Contains("visitor"))return false;
            var pad=Pad();if(!sim.Networks.SafeLanding(pad,out _))return false;
            bool Ready(string type)=>sim.State.structures.Any(b=>b.definition==type&&sim.Operating(b)&&sim.Networks.Connected(pad.id,b.id)&&(sim.Definition(b).staff==0||b.serviceTime>0&&sim.State.time-b.serviceTime<sim.Day&&sim.SurfaceActors.Any(a=>a.workplace==b.id)));
            if(!Ready("reception")||!Ready("restaurant")||!Ready("attraction")&&!Ready("garden"))return false;
            int activeGuests=sim.State.actors.Count(a=>a.kind==ActorKind.Visitor&&a.location!=PersonLocation.Departed&&a.location!=PersonLocation.Dead);
            var beds=sim.People.FreeBeds(true);int count=Mathf.Min(beds.Count,sim.State.visitorCap-activeGuests,2+Mathf.FloorToInt(sim.State.Random()*5));if(count<2)return false;
            var flight=New(FlightKind.Visitors,"Visitor shuttle");for(int i=0;i<count;i++){var a=sim.AddPerson(Profession.Service,true);a.location=PersonLocation.Reserved;a.home=beds[i];a.background="Independent galactic traveller";flight.passengers.Add(a.id);flight.beds.Add(beds[i]);}return true;
        }
        void Departures()
        {
            var booked=new HashSet<string>(sim.State.flights.Where(f=>f.kind==FlightKind.Departure&&Active(f)).SelectMany(f=>f.passengers));
            var leaving=sim.SurfaceActors.Where(a=>a.kind==ActorKind.Visitor&&a.returnAfter<=sim.State.time&&!booked.Contains(a.id)).Take(6).ToArray();
            if(leaving.Length==0||Pad()==null)return;var flight=New(FlightKind.Departure,"Visitor departure");flight.passengers=leaving.Select(a=>a.id).ToList();
        }
    }
}
