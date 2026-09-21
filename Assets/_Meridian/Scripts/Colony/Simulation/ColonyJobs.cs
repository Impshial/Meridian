using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    /// <summary>Persistent work orders. Claims cover stock at source and space at destination, never imaginary cargo.</summary>
    public sealed class ColonyJobs
    {
        readonly ColonySimulation sim;float planClock;
        StructureState[] chargingPoints=Array.Empty<StructureState>();
        public ColonyJobs(ColonySimulation owner){sim=owner;}
        public bool Active(JobState j)=>j.stage!=JobStage.Complete&&j.stage!=JobStage.Cancelled;
        public bool Eligible(ActorState a,JobState j)
        {
            if(a.location!=PersonLocation.Arrived||a.paused||a.health<=0||a.condition<=.15f||a.training)return false;
            var b=sim.Structure(j.target);bool industrial=j.machineOnly||(b!=null&&sim.Definition(b).machineOnly);
            if(a.kind==ActorKind.Colonist)return !industrial&&(a.profession==Profession.Builder&&(j.kind==JobKind.Build||j.kind==JobKind.Repair||j.kind==JobKind.Dismantle)||a.profession==Profession.Technician&&j.kind==JobKind.Repair)&&a.hunger<65&&a.thirst<65&&a.fatigue<sim.People.RestThreshold+5&&sim.People.CanEquipBuilder(a,b);
            if(a.kind==ActorKind.Visitor||a.charge<20)return false;
            if(j.kind==JobKind.Harvest){var obj=sim.World.Object(j.target);return obj!=null&&(obj.Kind==SurfaceObjectKind.Tree?a.kind==ActorKind.ForestryBot:a.kind==ActorKind.WorkRobot);}
            if(j.kind==JobKind.Haul)return a.kind==ActorKind.Drone||a.kind==ActorKind.WorkRobot||a.kind==ActorKind.ForestryBot;
            if(j.kind==JobKind.Rescue)return a.kind==ActorKind.WorkRobot||a.kind==ActorKind.Drone;
            return a.kind==ActorKind.WorkRobot;
        }
        public JobState Create(JobKind kind,string target,int priority=1,bool machineOnly=false)
        {var j=new JobState{id=sim.State.Id("job"),kind=kind,target=target,priority=priority,machineOnly=machineOnly};sim.State.jobs.Add(j);return j;}
        bool Exists(JobKind kind,string target)=>sim.State.jobs.Any(j=>Active(j)&&j.kind==kind&&j.target==target);
        public IEnumerable<InventoryState> Sources(Good good,InventoryState destination)
        {
            return sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&!b.paused&&b.definition!="cargo"||b.phase==BuildPhase.Complete&&b.definition=="cargo")
                .SelectMany(b=>new[]{sim.Stock.Get(b.inventory),sim.Stock.Get(b.waterInventory)})
                .Where(i=>i!=null&&i!=destination&&sim.Stock.Available(i,good)>.001f)
                .OrderBy(i=>Vector3.SqrMagnitude(sim.InventoryPosition(i.id)-sim.InventoryPosition(destination.id)));
        }
        public void Supply(StructureState target,Good good,float desired,int priority=1,InventoryState destination=null)
        {
            destination=destination??sim.Stock.Get(target.inventory);if(destination==null)return;
            float missing=desired-sim.Stock.Count(destination,good)-sim.Stock.Incoming(destination,good);if(missing<Mathf.Min(2,desired*.25f))return;
            foreach(var source in Sources(good,destination))
            {
                // Inputs already committed to a production batch have left its buffer. Other facilities retain their working stock.
                var sourceBuilding=sim.Structure(source.owner);
                if(sourceBuilding!=null&&!Storage(sourceBuilding)&&sourceBuilding.definition!="cargo"&&sourceBuilding.phase==BuildPhase.Complete)
                {
                    var recipe=sim.Catalog.Recipe(sourceBuilding.recipe);
                    if(recipe!=null&&recipe.inputs.Any(i=>i.good==good))continue;
                    if((sourceBuilding.definition=="canteen"||sourceBuilding.definition=="restaurant")&&good==Good.Food)continue;
                    if(sourceBuilding.definition=="greenhouse"&&good==Good.Biomass)continue;
                }
                float amount=Mathf.Min(missing,60,sim.Stock.Available(source,good),sim.Stock.Free(destination,good));if(amount<.01f)continue;
                var job=Create(JobKind.Haul,target.id,priority,true);job.source=source.id;job.destination=destination.id;job.good=good;job.quantity=amount;
                if(!sim.Stock.Reserve(job.id,source,destination,good,amount)){sim.State.jobs.Remove(job);continue;}
                missing-=amount;if(missing<.01f)break;
            }
        }
        public bool Storage(StructureState b)=>b.definition=="ship"||b.definition=="stockyard"||b.definition=="warehouse"||b.definition=="tank";
        public InventoryState StorageFor(Good good,Vector3 position,string except=null)
        {
            return sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&b.enabled&&!b.paused&&Storage(b))
                .SelectMany(b=>new[]{sim.Stock.Get(b.inventory),sim.Stock.Get(b.waterInventory)})
                .Where(i=>i!=null&&i.id!=except&&sim.Stock.Free(i,good)>.01f).OrderBy(i=>Vector3.SqrMagnitude(sim.InventoryPosition(i.id)-position)).FirstOrDefault();
        }
        public void Cancel(JobState j,string reason="Cancelled")
        {
            if(!Active(j))return;var a=sim.Actor(j.actor);sim.Stock.Release(j.id);
            if(a!=null){a.job=null;sim.Navigation.Stop(a);a.reason=reason;if(a.eva)a.returning=true;}
            var delta=sim.World.Existing(j.target);if(delta!=null&&delta.claim==j.id)delta.claim=null;
            j.actor=null;j.stage=JobStage.Cancelled;j.blocker=reason;
        }
        void Finish(JobState j,ActorState a)
        {sim.Stock.Release(j.id);j.stage=JobStage.Complete;j.blocker="Complete";a.job=null;a.activity="Idle";a.actionTime=0;var d=sim.World.Existing(j.target);if(d!=null&&d.claim==j.id)d.claim=null;sim.Notify(j.target);}
        void Retry(JobState j,ActorState a,string reason)
        {
            if(reason=="Planning route"){j.retryAt=(float)sim.State.time+.2f;return;}
            // A loaded carrier keeps its incoming reservation and the same job. Nothing teleports back to source.
            sim.Navigation.Stop(a);j.retries++;j.retryAt=(float)sim.State.time+Mathf.Min(60,4*Mathf.Pow(2,Mathf.Min(4,j.retries-1)));j.blocker=reason;a.reason=reason;
            if(!j.carrying){a.job=null;j.actor=null;j.stage=JobStage.Waiting;if(a.eva)a.returning=true;}
        }
        void Plan()
        {
            foreach(var b in sim.State.structures.ToArray())
            {
                if(b.paused||b.phase==BuildPhase.Removed)continue;
                var d=sim.Definition(b);
                if(b.phase==BuildPhase.Clearing)
                {
                    var blockers=ColonyCommands.Obstacles(sim,b.definition,b.position,b.yaw,b.end).Where(o=>!sim.World.Removed(o.Id)&&o.Id!=b.deposit).ToArray();
                    foreach(var obj in blockers){var delta=sim.World.Change(obj.Id);delta.designated=true;delta.priority=Mathf.Max(delta.priority,b.priority+1);}
                    b.blocker=blockers.Length>0?"Clearing resources: "+blockers.Length:"Awaiting material delivery";
                    if(blockers.Length==0)b.phase=BuildPhase.Delivery;
                }
                if(b.phase==BuildPhase.Delivery)
                {
                    var cost=ColonyCommands.Cost(sim,b);foreach(var item in cost)Supply(b,item.good,item.quantity,b.priority+2);
                    if(sim.Stock.CanConsume(sim.Stock.Get(b.inventory),cost))
                    {sim.Stock.Consume(sim.Stock.Get(b.inventory),cost);b.incorporated=cost;b.phase=BuildPhase.Construction;b.blocker="Waiting for construction labor";}
                    else b.blocker="Materials: "+string.Join(", ",cost.Where(c=>sim.Stock.Count(sim.Stock.Get(b.inventory),c.good)<c.quantity).Select(c=>c.good+" "+Mathf.CeilToInt(c.quantity-sim.Stock.Count(sim.Stock.Get(b.inventory),c.good))));
                }
                if(b.phase==BuildPhase.Construction&&!Exists(JobKind.Build,b.id))Create(JobKind.Build,b.id,b.priority,d.machineOnly);
                if(b.phase==BuildPhase.Dismantling&&!Exists(JobKind.Dismantle,b.id))Create(JobKind.Dismantle,b.id,b.priority,d.machineOnly);
                if(b.phase==BuildPhase.Complete&&b.condition<sim.Catalog.balance.maintenanceThreshold&&b.definition!="cargo")
                {
                    Supply(b,Good.Metal,1,10);Supply(b,Good.Components,1,10);
                    if(!Exists(JobKind.Repair,b.id))Create(JobKind.Repair,b.id,b.definition=="air"?100:10,d.machineOnly);
                }
            }
            foreach(var delta in sim.State.resources.Where(r=>r.designated&&!r.removed&&!r.paused).ToArray())
                if(!Exists(JobKind.Harvest,delta.id)){var j=Create(JobKind.Harvest,delta.id,delta.priority,true);delta.claim=j.id;}
            foreach(var a in sim.SurfaceActors.Where(a=>a.kind!=ActorKind.Colonist&&a.kind!=ActorKind.Visitor&&(a.charge<=.01f||a.condition<=.15f)).ToArray())
                if(!Exists(JobKind.Rescue,a.id))Create(JobKind.Rescue,a.id,50,true);
            // Completed jobs are history, not the live scheduler. Keep a useful bounded history.
            var history=sim.State.jobs.Where(j=>!Active(j)).ToArray();if(history.Length>200)foreach(var j in history.Take(history.Length-200))sim.State.jobs.Remove(j);
        }
        public void Tick(float dt)
        {
            chargingPoints=sim.State.structures.Where(b=>(b.definition=="ship"||b.definition=="charger"||b.definition=="repair")&&sim.Operating(b)).ToArray();
            planClock-=dt;if(planClock<=0){Plan();planClock=1;}
            var available=sim.SurfaceActors.Where(a=>a.job==null&&!a.returning&&a.intention==null&&(a.kind==ActorKind.Colonist||sim.Stock.Used(sim.Stock.Get(a.inventory))<.01f)).ToArray();
            foreach(var j in sim.State.jobs.Where(Active).OrderByDescending(j=>j.priority).ToArray())
            {
                if(j.paused||sim.State.time<j.retryAt)continue;var b=sim.Structure(j.target);
                if(b!=null&&(b.paused||b.phase==BuildPhase.Removed)){if(b.phase==BuildPhase.Removed)Cancel(j,"Target removed");continue;}
                var a=sim.Actor(j.actor);
                if(a==null)
                {
                    bool reserveForBuilder=sim.State.preferHumanBuilders&&!j.machineOnly&&(j.kind==JobKind.Build||j.kind==JobKind.Repair||j.kind==JobKind.Dismantle)&&sim.SurfaceActors.Any(x=>x.kind==ActorKind.Colonist&&Eligible(x,j));
                    int preference=int.MaxValue;float distance=float.PositiveInfinity;Vector3 target=sim.Position(j.target);
                    foreach(var candidate in available)
                    {
                        if(candidate.job!=null||reserveForBuilder&&candidate.kind!=ActorKind.Colonist||!Eligible(candidate,j))continue;
                        if(candidate.kind!=ActorKind.Colonist&&candidate.charge<RequiredCharge(candidate,j))
                        {candidate.reason="Insufficient charge for work and return; add a nearer powered charger for remote work";if(candidate.charge<99)candidate.intention="charge";continue;}
                        int rank=Preference(candidate,j);float square=(candidate.position-target).sqrMagnitude;
                        if(rank<preference||rank==preference&&square<distance){a=candidate;preference=rank;distance=square;}
                    }
                    if(a==null){j.retryAt=(float)sim.State.time+.5f;continue;}a.job=j.id;j.actor=a.id;a.actionTime=0;
                }
                if(a.health<=0||a.paused){Cancel(j,"Worker unavailable; cargo remains recoverable");continue;}
                if(a.kind==ActorKind.Colonist)
                {
                    if(!Eligible(a,j)||a.suit<30&&a.eva){Retry(j,a,"Builder returning to safety");a.returning=true;continue;}
                    if(!sim.People.PrepareBuilder(a,b,dt))continue;
                }
                else if(a.charge<ReturnCharge(a,a.position)||a.condition<=.15f){Cancel(j,"Returning for charge or service; carried cargo retained");a.intention="charge";continue;}
                if(j.kind==JobKind.Haul)Haul(j,a);
                else if(j.kind==JobKind.Harvest)Harvest(j,a,dt);
                else if(j.kind==JobKind.Rescue)Rescue(j,a);
                else Work(j,a,b,dt);
            }
        }
        public float RequiredCharge(ActorState actor,JobState job)
        {
            if(chargingPoints.Length==0)chargingPoints=sim.State.structures.Where(b=>(b.definition=="ship"||b.definition=="charger"||b.definition=="repair")&&sim.Operating(b)).ToArray();
            Vector3 target=sim.Position(job.target);float travel=Vector3.Distance(actor.position,target);
            if(job.kind==JobKind.Haul){var source=sim.InventoryPosition(job.source);target=sim.InventoryPosition(job.destination);travel=Vector3.Distance(actor.position,source)+Vector3.Distance(source,target);}
            var building=sim.Structure(job.target);float work=building!=null?sim.Definition(building).buildSeconds*(sim.Definition(building).link?Mathf.Max(1,building.length/10):1):60;
            return Mathf.Max(20,TravelCharge(actor,travel)+ReturnCharge(actor,target)+Mathf.Min(10,work*.02f));
        }
        float TravelCharge(ActorState actor,float metres)=>metres/(actor.kind==ActorKind.Drone?18:8)*.016f*1.5f;
        float ReturnCharge(ActorState actor,Vector3 from)
        {float distance=float.PositiveInfinity;foreach(var charger in chargingPoints)distance=Mathf.Min(distance,Vector3.Distance(from,charger.position));return 8+TravelCharge(actor,distance);}
        int Preference(ActorState a,JobState j)
        {if(j.kind==JobKind.Haul)return a.kind==ActorKind.Drone?0:2;if(a.kind==ActorKind.Colonist)return sim.State.preferHumanBuilders?0:1;return sim.State.preferHumanBuilders?1:0;}
        bool Travel(JobState j,ActorState a,Vector3 position,string target=null)
        {
            if(Vector3.Distance(a.position,position)<2)return true;
            if(sim.Navigation.Arrived(a))
            {if(!sim.Navigation.Go(a,position,target)){Retry(j,a,a.reason);return false;}j.retries=0;a.activity="Travelling: "+j.kind;}
            return false;
        }
        void Haul(JobState j,ActorState a)
        {
            if(!sim.Navigation.Arrived(a))return;
            var cargo=sim.Stock.Get(a.inventory);var destination=sim.Stock.Get(j.destination);if(destination==null){Cancel(j,"Destination removed");return;}
            if(!j.carrying)
            {
                j.stage=JobStage.Pickup;var source=sim.Stock.Get(j.source);if(source==null){Cancel(j,"Source removed");return;}
                var owner=sim.Structure(source.owner);Vector3 point=owner!=null?sim.Navigation.ServicePoint(owner,a.position):sim.InventoryPosition(source.id);
                if(!Travel(j,a,point,source.owner))return;
                float moved=sim.Stock.Transfer(source,cargo,j.good,j.quantity,j.id);sim.Stock.Release(j.id,true);
                if(moved<.001f){Cancel(j,"Reserved supplies unavailable");return;}
                j.quantity=moved;j.carrying=true;j.stage=JobStage.Delivering;sim.Navigation.Stop(a);
                foreach(var reservation in sim.State.reservations.Where(r=>r.job==j.id&&r.incoming))reservation.quantity=moved;
            }
            var destinationOwner=sim.Structure(destination.owner);var target=destinationOwner!=null?sim.Navigation.ServicePoint(destinationOwner,a.position):sim.InventoryPosition(destination.id);
            if(!Travel(j,a,target,destination.owner))return;
            float delivered=sim.Stock.Transfer(cargo,destination,j.good,j.quantity,j.id);j.quantity-=delivered;
            if(j.quantity<.001f)Finish(j,a);else Retry(j,a,"Delivery space unavailable; cargo remains aboard");
        }
        void Harvest(JobState j,ActorState a,float dt)
        {
            if(!sim.Navigation.Arrived(a))return;
            var obj=sim.World.Object(j.target);if(obj==null||sim.World.Removed(j.target)){Finish(j,a);return;}
            var cargo=sim.Stock.Get(a.inventory);Good good=sim.World.Yield(obj);
            if(j.carrying)
            {
                var storage=sim.Stock.Get(j.destination)??StorageFor(good,a.position);if(storage==null){j.blocker="No available storage";return;}
                j.destination=storage.id;var b=sim.Structure(storage.owner);var point=b!=null?sim.Navigation.ServicePoint(b,a.position):sim.InventoryPosition(storage.id);
                if(!Travel(j,a,point,storage.owner))return;sim.Stock.Transfer(cargo,storage,good,sim.Stock.Count(cargo,good));
                if(sim.Stock.Count(cargo,good)<.001f)Finish(j,a);else{j.destination=null;j.retryAt=(float)sim.State.time+4;}return;
            }
            if(!Travel(j,a,sim.World.Ground(obj.Position+new Vector3(obj.Radius+1,0,0))))return;
            j.stage=JobStage.Working;a.activity="Harvesting "+good;j.blocker="Working";
            float rate=obj.Kind==SurfaceObjectKind.Tree?12:8;sim.Harvest(j.target,cargo,dt*rate);a.charge=Mathf.Max(0,a.charge-dt*.012f);j.progress+=dt;
            if(sim.Stock.Free(cargo,good)<.01f||sim.World.Remaining(j.target)<.01f){j.carrying=true;j.stage=JobStage.Delivering;sim.Navigation.Stop(a);}
        }
        void Work(JobState j,ActorState a,StructureState b,float dt)
        {
            if(!sim.Navigation.Arrived(a))return;
            if(b==null){Cancel(j,"Site no longer exists");return;}
            if(a.kind==ActorKind.Colonist&&sim.Definition(b).machineOnly){Cancel(j,"Industrial tasks require machines");return;}
            Vector3 point=a.kind==ActorKind.Colonist&&!a.eva?b.position:sim.Navigation.ServicePoint(b,a.position);
            if(!Travel(j,a,point,b.id))return;
            j.stage=JobStage.Working;j.blocker="Working";a.activity=j.kind+" · "+b.name;
            float work=dt*(a.kind==ActorKind.Colonist?a.skill:sim.State.completedResearch.Contains("automation")?1.2f:1);if(a.kind==ActorKind.Colonist)a.experience+=dt*.01f;else a.charge=Mathf.Max(0,a.charge-dt*.02f);
            if(j.kind==JobKind.Build)
            {
                b.progress=Mathf.Clamp01(b.progress+work/Mathf.Max(1,sim.Definition(b).buildSeconds*(sim.Definition(b).link?Mathf.Max(1,b.length/10):1)));
                if(b.progress>=1){b.phase=BuildPhase.Complete;b.blocker="Commissioned; connect utilities";sim.Networks.Dirty();Finish(j,a);if(a.eva)a.returning=true;}
            }
            else if(j.kind==JobKind.Repair)
            {
                if(j.progress==0){var materials=new[]{new Amount(Good.Metal,1),new Amount(Good.Components,1)};if(!sim.Stock.Consume(sim.Stock.Get(b.inventory),materials)){j.blocker="Waiting for repair parts";return;}}
                j.progress+=work/10;if(j.progress>=1){b.condition=1;sim.Networks.Dirty();Finish(j,a);}
            }
            else if(j.kind==JobKind.Dismantle)
            {j.progress+=work/12;if(j.progress>=1){ColonyCommands.Recover(sim,b);Finish(j,a);}}
        }
        void Rescue(JobState j,ActorState a)
        {
            var target=sim.Actor(j.target);if(target==null||target.charge>5&&target.condition>.15f){Finish(j,a);return;}
            if(!j.carrying){if(!Travel(j,a,target.position))return;j.carrying=true;sim.Navigation.Stop(a);}
            var pad=sim.State.structures.Where(b=>(b.definition=="repair"||b.definition=="ship"||b.definition=="charger")&&sim.Operating(b)).OrderBy(b=>Vector3.SqrMagnitude(b.position-a.position)).FirstOrDefault();
            if(pad==null){j.blocker="No powered recovery/charging facility";return;}
            target.position=a.position+Vector3.right*2;target.activity="Being towed to charger";
            if(Travel(j,a,sim.Navigation.ServicePoint(pad,a.position),pad.id)){target.intention="charge";target.target=pad.id;target.charge=.1f;Finish(j,a);}
        }
    }
}
