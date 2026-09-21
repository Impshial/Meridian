using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed class ColonyPeople
    {
        readonly ColonySimulation sim;float planning;
        public ColonyPeople(ColonySimulation owner){sim=owner;}
        bool Person(ActorState a)=>a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor;
        public int StaffCapacity(StructureState b)=>b.staffingOverride?Mathf.Clamp(b.staffLimit,0,(int)sim.Definition(b).staff):(int)sim.Definition(b).staff;
        public float Staffing(StructureState b)=>Mathf.Min(StaffCapacity(b),b.staffed);
        public float RestThreshold=>sim.State.workPolicy==1?48:sim.State.workPolicy==2?72:60;
        public float TrainingTime=>sim.Day/24*sim.Catalog.balance.trainingHours*(sim.State.completedResearch.Contains("civic")?.5f:1);
        public int BedsUsed(string id)=>sim.State.actors.Count(a=>a.home==id&&a.location!=PersonLocation.Departed&&a.location!=PersonLocation.Dead);
        public List<string> FreeBeds(bool guests)
        {
            var beds=new List<string>();foreach(var b in sim.State.structures.Where(b=>b.definition==(guests?"lodge":"habitat")&&sim.Operating(b)))
                for(int i=BedsUsed(b.id);i<sim.Definition(b).beds;i++)beds.Add(b.id);return beds;
        }
        bool Accessible(ActorState a,StructureState b)=>sim.Operating(b)&&(sim.OutdoorsSafe||sim.Networks.Connected(a.building,b.id));
        public bool CanEquipBuilder(ActorState a,StructureState site)
        {
            if(site==null||sim.Definition(site).machineOnly)return false;
            if(sim.OutdoorsSafe||a.eva||site.phase==BuildPhase.Complete&&Accessible(a,site))return true;
            if(!sim.State.structures.Any(b=>b.definition=="airlock"&&Accessible(a,b)))return false;
            foreach(var good in new[]{Good.EVASuit,Good.Toolkit})
                if(sim.Stock.Count(sim.Stock.Get(a.equipment),good)<1&&!sim.State.structures.Any(b=>Accessible(a,b)&&sim.Stock.Available(sim.Stock.Get(b.inventory),good)>=1))return false;
            return true;
        }
        public bool PrepareBuilder(ActorState a,StructureState site,float dt=.1f)
        {
            if(site==null||sim.Definition(site).machineOnly)return false;
            if(a.returning){ReturnBuilder(a);return false;}
            if(sim.OutdoorsSafe)return true;
            if(site.phase==BuildPhase.Complete&&Accessible(a,site))return true;
            if(a.eva)return true;
            var gear=sim.Stock.Get(a.equipment);
            foreach(Good good in new[]{Good.EVASuit,Good.Toolkit})
            {
                if(sim.Stock.Count(gear,good)>.9f)continue;
                var stock=sim.State.structures.Where(b=>sim.Operating(b)&&sim.Networks.Connected(a.building,b.id)&&sim.Stock.Available(sim.Stock.Get(b.inventory),good)>=1)
                    .OrderBy(b=>Vector3.SqrMagnitude(b.position-a.position)).FirstOrDefault();
                if(stock==null){a.reason="Civilian EVA needs a reusable suit and toolkit in reachable storage";return false;}
                if(Vector3.Distance(a.position,stock.position)>2){if(sim.Navigation.Arrived(a))sim.Navigation.Go(a,stock.position,stock.id);a.activity="Collecting EVA equipment";return false;}
                sim.Stock.Transfer(sim.Stock.Get(stock.inventory),gear,good,1);return false;
            }
            var airlock=sim.State.structures.Where(b=>b.definition=="airlock"&&Accessible(a,b)).OrderBy(b=>Vector3.SqrMagnitude(b.position-site.position)).FirstOrDefault();
            if(airlock==null){a.reason="Builder requires a powered, pressurized airlock";return false;}
            if(sim.State.environment.storm>.65f){a.reason="EVA suspended during severe weather";return false;}
            if(Vector3.Distance(a.position,airlock.position)>2){if(sim.Navigation.Arrived(a))sim.Navigation.Go(a,airlock.position,airlock.id);a.activity="Moving to airlock";return false;}
            if(gear.items.Any(i=>i.condition<.15f)||a.gearRepair)
            {
                sim.Jobs.Supply(airlock,Good.Components,2,9);
                if(!a.gearRepair)
                {if(!sim.Stock.Consume(sim.Stock.Get(airlock.inventory),Good.Components,1)){a.reason="Waiting for a component to service EVA equipment at the airlock";return false;}a.gearRepair=true;a.actionTime=0;}
                a.activity="Airlock · servicing suit and toolkit";a.actionTime+=dt;
                if(a.actionTime>=5){foreach(var item in gear.items)item.condition=1;a.gearRepair=false;a.actionTime=0;}
                return false;
            }
            a.actionTime+=dt;a.activity="Airlock · preparing EVA";if(a.actionTime<2)return false;
            a.actionTime=0;a.eva=true;a.airlock=airlock.id;a.suit=100;
            a.position=sim.Navigation.Door(airlock,site.position)+Vector3.Normalize(site.position-airlock.position)*2;sim.Navigation.Stop(a);return true;
        }
        void ReturnBuilder(ActorState a)
        {
            if(!a.eva){a.returning=false;return;}
            var door=sim.Structure(a.airlock);
            if(door==null||!sim.Operating(door))door=sim.State.structures.Where(b=>b.definition=="airlock"&&sim.Operating(b)).OrderBy(b=>Vector3.SqrMagnitude(a.position-b.position)).FirstOrDefault();
            if(door==null){a.reason="Emergency: no operational return airlock";return;}
            if(Vector3.Distance(a.position,door.position)>2){if(sim.Navigation.Arrived(a))sim.Navigation.Go(a,door.position,door.id,true);a.activity="Returning through airlock";return;}
            a.building=door.id;a.eva=false;a.returning=false;a.suit=100;a.actionTime=0;
            foreach(var gear in sim.Stock.Get(a.equipment).items)gear.condition=Mathf.Max(0,gear.condition-.003f);sim.Navigation.Stop(a);
        }
        void Plan()
        {
            foreach(var b in sim.State.structures)foreach(var excess in sim.SurfaceActors.Where(a=>a.workplace==b.id).Skip(StaffCapacity(b)).ToArray()){excess.workplace=null;if(excess.intention=="work")excess.intention=null;}
            foreach(var a in sim.SurfaceActors.Where(a=>a.kind==ActorKind.Colonist))
            {
                if(a.home==null||!sim.Operating(sim.Structure(a.home))){var free=FreeBeds(false);if(free.Count>0)a.home=free[0];}
                if(a.training)continue;
                var assigned=sim.Structure(a.workplace);if(assigned!=null&&(!sim.Operating(assigned)||sim.Definition(assigned).profession!=a.profession)){a.workplace=null;if(a.intention=="work")a.intention=null;}
                if(a.workplace==null&&a.profession!=Profession.Builder)
                {
                    var workplace=sim.State.structures.Where(b=>StaffCapacity(b)>0&&sim.Definition(b).profession==a.profession&&Accessible(a,b)&&sim.State.actors.Count(p=>p.workplace==b.id&&p.location==PersonLocation.Arrived)<StaffCapacity(b))
                        .OrderBy(b=>Vector3.SqrMagnitude(b.position-a.position)).FirstOrDefault();if(workplace!=null)a.workplace=workplace.id;
                }
            }
            foreach(var b in sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&!b.paused))
            {
                if(b.definition=="canteen"||b.definition=="restaurant")sim.Jobs.Supply(b,Good.Food,Mathf.Min(80,Mathf.Max(12,sim.Population*3)),6);
                if(b.definition=="clinic")sim.Jobs.Supply(b,Good.Medicine,8,7);
                if(b.definition=="greenhouse"||b.definition=="field")sim.Jobs.Supply(b,Good.Biomass,12,6);
            }
        }
        public void Tick(float dt)
        {
            planning-=dt;if(planning<=0){Plan();planning=2;}
            foreach(var b in sim.State.structures)b.staffed=0;
            // Establish attendance before serving anyone, independent of actor list order.
            // Otherwise a patient/student processed before the medic/teacher waits forever.
            foreach(var worker in sim.SurfaceActors)
            {
                if(worker.kind!=ActorKind.Colonist||worker.intention!="work"||worker.target!=worker.workplace||!sim.Navigation.Arrived(worker)||worker.health<=0)continue;
                var workplace=sim.Structure(worker.workplace);
                if(sim.Operating(workplace)&&Vector3.Distance(worker.position,sim.Navigation.InteriorPoint(workplace,worker,"work"))<=2)workplace.staffed+=worker.skill/.70f;
            }
            foreach(var a in sim.SurfaceActors.ToArray())
            {
                if(!Person(a)){Machine(a,dt);continue;}
                if(a.health<=0){Die(a);continue;}
                float day=dt/sim.Day;var pack=sim.Stock.Get(a.inventory);
                a.hunger=Mathf.Min(100,a.hunger+day*100);a.thirst=Mathf.Min(100,a.thirst+day*100);a.fatigue=Mathf.Min(100,a.fatigue+day*32);a.hygiene=Mathf.Max(0,a.hygiene-day*18);a.recreation=Mathf.Max(0,a.recreation-day*18);
                float food=sim.Catalog.balance.foodPerPersonDay*day,water=sim.Catalog.balance.waterPerPersonDay*day;
                if(sim.Stock.Consume(pack,Good.Food,food))a.hunger=Mathf.Max(0,a.hunger-day*100);
                if(sim.Stock.Consume(pack,Good.Water,water))a.thirst=Mathf.Max(0,a.thirst-day*100);
                if(a.hunger>90||a.thirst>90)a.health=Mathf.Max(0,a.health-day*18);
                if(a.hygiene<10)a.health=Mathf.Max(0,a.health-day*2);
                if(a.eva){a.suit=Mathf.Max(0,a.suit-dt*.03f);if(a.suit<=0)a.health=Mathf.Max(0,a.health-dt*.4f);if(a.suit<35||sim.State.environment.storm>.7f)a.returning=true;}
                a.morale=Mathf.MoveTowards(a.morale,(100-a.hunger+100-a.thirst+100-a.fatigue+a.hygiene+a.recreation)*.2f,dt*.3f);
                if(a.returning){if(a.job!=null){var job=sim.State.jobs.Find(j=>j.id==a.job);if(job!=null)sim.Jobs.Cancel(job,"Builder returning for safety/needs");}ReturnBuilder(a);continue;}
                if(a.job!=null)continue;
                if(a.eva){a.returning=true;ReturnBuilder(a);continue;}
                var current=sim.Structure(a.building);
                if(!sim.OutdoorsSafe&&current!=null&&current.air<.2f)
                {
                    var safe=sim.State.structures.Where(b=>sim.Networks.Pressurized(b.id)&&b.air>.5f&&sim.Networks.Connected(a.building,b.id)).OrderByDescending(b=>b.air).FirstOrDefault();
                    if(safe!=null){Begin(a,"evacuate",safe);a.reason="Evacuating low air reserve";}else a.reason="Isolated module: repair connection before air runs out";
                }
                if(a.intention==null&&sim.State.time>=a.nextDecision){Decide(a,pack);a.nextDecision=(float)sim.State.time+1;}
                Act(a,dt);
            }
        }
        void Decide(ActorState a,InventoryState pack)
        {
            StructureState Find(Func<StructureState,bool> filter)=>sim.State.structures.Where(b=>Accessible(a,b)&&filter(b)).OrderBy(b=>Vector3.SqrMagnitude(b.position-a.position)).FirstOrDefault();
            if(sim.Stock.Count(pack,Good.Food)<.5f||a.hunger>40)
            {
                var food=Find(b=>(a.kind==ActorKind.Visitor?b.definition=="restaurant":b.definition=="canteen"||b.definition=="ship")&&sim.Stock.Available(sim.Stock.Get(b.inventory),Good.Food)>1);
                if(food==null&&a.hunger>40)food=Find(b=>b.definition=="ship"&&sim.Stock.Available(sim.Stock.Get(b.inventory),Good.Food)>1);
                if(food!=null){Begin(a,"meal",food);return;}a.reason="No reachable meal supply";
            }
            if(sim.Stock.Count(pack,Good.Water)<.5f||a.thirst>40)
            {var tap=Find(b=>sim.Definition(b).sealedModule&&sim.Networks.WaterAvailable(b.id)>1);if(tap!=null){Begin(a,"drink",tap);return;}a.reason="No reachable water supply";}
            if(a.health<70){var clinic=Find(b=>b.definition=="clinic"&&sim.Stock.Count(sim.Stock.Get(b.inventory),Good.Medicine)>.1f);if(clinic!=null){Begin(a,"treat",clinic);return;}}
            if(a.fatigue>RestThreshold){var home=sim.Structure(a.home);if(home!=null&&Accessible(a,home)){Begin(a,"sleep",home);return;}}
            if(a.hygiene<45){var bath=Find(b=>b.definition=="sanitation"&&sim.Networks.WaterAvailable(b.id)>1);if(bath!=null){Begin(a,"wash",bath);return;}}
            if(a.recreation<50||a.kind==ActorKind.Visitor&&sim.State.time-a.serviceCredit>sim.Day*.3f)
            {
                var venue=Find(b=>(b.definition=="lounge"||b.definition=="attraction"||b.definition=="garden"||b.definition=="park")&&(b.definition!="park"||sim.OutdoorsSafe));
                if(venue!=null){Begin(a,"recreation",venue);return;}
            }
            if(a.training)
            {
                var school=sim.Structure(a.trainingCenter);
                if(school==null||school.phase==BuildPhase.Removed){a.training=false;a.reason="Training stopped: centre removed";}
                else{if(Accessible(a,school))Begin(a,"training",school);else a.reason="Training waiting for a reachable, supplied centre";return;}
            }
            var work=sim.Structure(a.workplace);if(a.kind==ActorKind.Colonist&&work!=null&&Accessible(a,work)){Begin(a,"work",work);return;}
            a.activity=a.kind==ActorKind.Visitor?"Exploring colony":"Available for work";
        }
        void Begin(ActorState a,string intention,StructureState target)
        {
            if(a.intention==intention&&a.target==target.id)return;
            if(intention=="meal"||intention=="treat"||intention=="wash"||intention=="recreation")
            {
                var definition=sim.Definition(target);float capacity=definition.seats>0?definition.seats:Mathf.Max(2,definition.staff*4);
                if(sim.State.completedResearch.Contains("civic"))capacity*=1.25f;
                if(sim.SurfaceActors.Count(p=>p.id!=a.id&&p.target==target.id&&p.intention==intention)>=capacity){a.reason="Waiting for service capacity at "+target.name;return;}
            }
            sim.Navigation.Stop(a);if(sim.Navigation.Go(a,sim.Navigation.InteriorPoint(target,a,intention),target.id)){a.intention=intention;a.actionTime=0;a.activity="Going to "+target.name;}
        }
        void Act(ActorState a,float dt)
        {
            if(a.intention=="depart")return; // The flight owns boarding navigation, not a service station.
            if(a.intention==null||!sim.Navigation.Arrived(a))return;var b=sim.Structure(a.target);
            if(b==null||!sim.Operating(b)){a.intention=null;return;}
            var station=sim.Navigation.InteriorPoint(b,a,a.intention);
            if(Vector3.Distance(a.position,station)>2)
            {a.actionTime=0;sim.Navigation.Go(a,station,b.id);return;}
            a.building=b.id;a.activity=a.intention+" · "+b.name;a.actionTime+=dt;
            var pack=sim.Stock.Get(a.inventory);float day=dt/sim.Day;
            switch(a.intention)
            {
                case "meal":
                    if(a.actionTime<2)return;
                    if(b.definition=="restaurant"&&Staffing(b)<.1f&&a.workplace!=b.id){a.reason="Waiting for restaurant service";return;}
                    if(a.kind==ActorKind.Visitor&&a.wallet<6*Price()){a.reason="Visitor meal budget exhausted";a.returnAfter=(float)sim.State.time;a.intention=null;return;}
                    if(sim.Stock.Transfer(sim.Stock.Get(b.inventory),pack,Good.Food,Mathf.Min(4,sim.Stock.Free(pack,Good.Food)))>0){a.hunger=Mathf.Max(0,a.hunger-30);if(a.kind==ActorKind.Visitor&&b.definition=="restaurant")Pay(a,6*Price(),"Visitor meals",b);}a.intention=null;break;
                case "drink":if(a.actionTime<1)return;if(sim.Networks.DrawWater(b.id,Mathf.Min(4,sim.Stock.Free(pack,Good.Water)),pack)>0)a.thirst=Mathf.Max(0,a.thirst-30);a.intention=null;break;
                case "sleep":a.fatigue=Mathf.Max(0,a.fatigue-dt*6);a.health=Mathf.Min(100,a.health+dt*.05f);if(a.fatigue<5){if(a.kind==ActorKind.Visitor)Pay(a,10*Price(),"Visitor lodging",b);a.intention=null;}break;
                case "wash":if(sim.Networks.DrawWater(b.id,day*2)>0)a.hygiene=Mathf.Min(100,a.hygiene+dt*7);if(a.hygiene>95)a.intention=null;break;
                case "treat":
                    if(Staffing(b)<.1f){a.reason="Waiting for medic";break;}
                    if(sim.Stock.Consume(sim.Stock.Get(b.inventory),Good.Medicine,dt*.01f))a.health=Mathf.Min(100,a.health+dt*(sim.State.completedResearch.Contains("medical")?3:2));if(a.health>95)a.intention=null;break;
                case "recreation":
                    if(a.actionTime<=dt&&a.kind==ActorKind.Visitor&&!Pay(a,8*Price(),"Visitor attractions",b)){a.intention=null;break;}
                    a.recreation=Mathf.Min(100,a.recreation+dt*5);if(a.recreation>95||a.actionTime>10){a.serviceCredit=(float)sim.State.time;a.intention=null;}break;
                case "evacuate":a.intention=null;break;
                case "training":
                    if(sim.People.Staffing(b)<.1f)a.reason="Waiting for a training technician";
                    else a.trainingProgress+=dt;
                    if(a.trainingProgress>=TrainingTime)
                    {a.profession=a.trainAs;a.workplace=null;a.skill=.85f;a.history.Add("Retrained as "+a.trainAs);a.training=false;a.trainingCenter=null;a.trainingProgress=0;a.intention=null;}
                    if(a.actionTime>8||a.fatigue>RestThreshold+5||a.hunger>60||a.thirst>60)a.intention=null;break;
                case "work":
                    b.serviceTime=(float)sim.State.time;
                    if(b.definition=="greenhouse"||b.definition=="field")b.tending=Mathf.Min(sim.Day*2,b.tending+dt*a.skill*sim.Day/Mathf.Max(1,sim.Catalog.balance.tendingSecondsPerDay));
                    a.experience+=day*.1f;a.skill=Mathf.Min(1.5f,.85f+a.experience*.02f);
                    if(a.actionTime>8||a.fatigue>RestThreshold+5||a.hunger>60||a.thirst>60)a.intention=null;break;
            }
        }
        float Price()=>sim.State.pricePolicy==0?.75f:sim.State.pricePolicy==2?1.5f:1;
        bool Pay(ActorState a,float amount,string category,StructureState building)
        {
            if(a.wallet+1e-4f<amount){a.reason="Visitor budget exhausted";a.returnAfter=(float)sim.State.time;return false;}
            a.wallet-=amount;sim.Credit(amount,"Visitors",category+" · "+a.name);return true;
        }
        void Machine(ActorState a,float dt)
        {
            if(a.paused)return;a.condition=Mathf.Max(0,a.condition-sim.Catalog.balance.wearPerDay*dt/sim.Day);
            if(a.job!=null)return;
            var cargo=sim.Stock.Get(a.inventory);
            if(sim.Stock.Used(cargo)>.001f&&a.charge>8&&a.intention!="charge")
            {
                if(!sim.Navigation.Arrived(a))return;
                var item=cargo.items[0];var dest=sim.Jobs.StorageFor(item.good,a.position);
                if(dest==null){a.reason="Cargo held: no storage capacity";return;}
                var owner=sim.Structure(dest.owner);var point=sim.Navigation.ServicePoint(owner,a.position);
                if(Vector3.Distance(a.position,point)>2){if(sim.Navigation.Arrived(a))sim.Navigation.Go(a,point,owner.id);a.activity="Storing carried "+item.good;return;}
                sim.Stock.Transfer(cargo,dest,item.good,item.quantity);return;
            }
            if(a.charge<35||a.condition<.7f||a.intention=="charge")
            {
                var charger=sim.Structure(a.target);
                if(a.intention!="charge"||charger==null||!sim.Operating(charger))
                {
                    charger=sim.State.structures.Where(b=>(b.definition=="ship"||b.definition=="charger"||b.definition=="repair")&&sim.Operating(b)&&sim.SurfaceActors.Count(x=>x.target==b.id&&x.intention=="charge")<2).OrderBy(b=>Vector3.SqrMagnitude(b.position-a.position)).FirstOrDefault();
                    if(charger==null){a.reason="No free powered charging point";return;}a.intention="charge";sim.Navigation.Stop(a);
                    if(!sim.Navigation.Go(a,sim.Navigation.ServicePoint(charger,a.position),charger.id)){a.intention=null;return;}
                }
                if(!sim.Navigation.Arrived(a)){a.activity="Travelling to charger";return;}
                if(Vector3.Distance(a.position,sim.Navigation.ServicePoint(charger,a.position))>2)
                {sim.Navigation.Go(a,sim.Navigation.ServicePoint(charger,a.position),charger.id);return;}
                a.activity="Charging · "+charger.name;a.charge=Mathf.Min(100,a.charge+dt*4);
                if(a.condition<.99f)
                {
                    sim.Jobs.Supply(charger,Good.Components,2,10);
                    if(a.serviceCredit<=0&&sim.Stock.Consume(sim.Stock.Get(charger.inventory),Good.Components,1))a.serviceCredit=1;
                    if(a.serviceCredit>0)a.condition=Mathf.Min(1,a.condition+dt*.03f);
                }
                if(a.charge>=99&&(a.condition>=.99f||sim.Stock.Count(sim.Stock.Get(charger.inventory),Good.Components)<1)){a.intention=null;a.serviceCredit=0;a.activity="Available";}return;
            }
            a.intention=null;a.activity="Available";
        }
        void Die(ActorState a)
        {
            if(a.job!=null){var job=sim.State.jobs.Find(j=>j.id==a.job);if(job!=null)sim.Jobs.Cancel(job,"Worker lost");}
            var pile=sim.DropCargo(a.position,Array.Empty<Amount>(),"Recovered belongings · "+a.name);
            foreach(var source in new[]{sim.Stock.Get(a.inventory),sim.Stock.Get(a.equipment)})if(source!=null)foreach(var item in source.items.ToArray())sim.Stock.Transfer(source,sim.Stock.Get(pile.inventory),item.good,item.quantity);
            a.location=PersonLocation.Dead;a.home=null;a.workplace=null;a.history.Add("Died on colony day "+Mathf.FloorToInt((float)sim.State.time/sim.Day));sim.Navigation.Stop(a);sim.Notify(a.id);
        }
        public bool ChangeProfession(ActorState a,Profession profession)
        {
            var center=sim.State.structures.FirstOrDefault(b=>b.definition=="training"&&Accessible(a,b)&&Staffing(b)>.1f);if(center==null)return false;
            if(a.kind!=ActorKind.Colonist||a.training||a.job!=null||a.eva)return false;
            a.training=true;a.trainingCenter=center.id;a.trainAs=profession;a.trainingProgress=0;a.workplace=null;a.intention=null;a.reason="Training for "+profession;sim.Navigation.Stop(a);return true;
        }
    }
}
