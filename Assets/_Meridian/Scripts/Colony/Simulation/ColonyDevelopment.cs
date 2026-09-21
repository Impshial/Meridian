using System;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed class ColonyDevelopment
    {
        readonly ColonySimulation sim;
        public ColonyDevelopment(ColonySimulation owner){sim=owner;}
        public string ResearchBlocker(string id)
        {
            var r=sim.Catalog.Research(id);if(r==null)return "Unknown research";if(sim.State.completedResearch.Contains(id))return "Already completed";
            var missing=r.prerequisites.Where(p=>!sim.State.completedResearch.Contains(p)).Select(p=>sim.Catalog.Research(p).name);return missing.Any()?"Requires "+string.Join(", ",missing):null;
        }
        public void QueueResearch(string id)
        {if(ResearchBlocker(id)!=null)throw new InvalidOperationException(ResearchBlocker(id));if(!sim.State.researchQueue.Contains(id))sim.State.researchQueue.Add(id);}
        public void RemoveResearch(string id)
        {if(sim.State.researchQueue.Count>0&&sim.State.researchQueue[0]==id){sim.State.researchPoints+=sim.State.researchProgress;sim.State.researchProgress=0;}sim.State.researchQueue.Remove(id);}
        public float RegionPrice=>sim.Catalog.balance.regionCost*(1+.15f*Mathf.Max(0,sim.State.regions.Count(r=>r.owned)-1));
        public string RegionBlocker(int x,int z)
        {
            if(!sim.State.completedResearch.Contains("regions"))return "Regional Survey research required";
            if(sim.State.expansion.active)return "Another region is being surveyed";
            if(sim.State.regions.Any(r=>r.owned&&r.x==x&&r.z==z))return "Already owned";
            if(!sim.State.regions.Any(r=>r.owned&&Mathf.Abs(r.x-x)+Mathf.Abs(r.z-z)==1))return "Region must share an edge with owned territory";
            if(sim.State.credits<RegionPrice)return "Insufficient acquisition credits";
            int land=0;for(int iz=-2;iz<=2;iz++)for(int ix=-2;ix<=2;ix++)if(sim.World.Surface.Sample(x*6000+ix*1000,z*6000+iz*1000).IsLand)land++;
            return land<8?"Predominantly ocean; unsuitable for this land colony":null;
        }
        public RegionOperation BeginRegion(int x,int z)
        {var error=RegionBlocker(x,z);if(error!=null)throw new InvalidOperationException(error);return sim.State.expansion=new RegionOperation{id=sim.State.Id("survey"),x=x,z=z,quotedPrice=RegionPrice,active=true};}
        public void CompleteRegion(RegionOperation operation,System.Collections.Generic.IEnumerable<SurfaceTileData> tiles)
        {
            if(!operation.active||sim.State.expansion.id!=operation.id)return;
            if(sim.State.credits<operation.quotedPrice)throw new InvalidOperationException("Acquisition cancelled: credits changed during survey");
            var generated=tiles.ToArray();if(generated.Length!=36)throw new InvalidOperationException("Incomplete regional survey");
            sim.World.AddTiles(generated);sim.Credit(-operation.quotedPrice,"Expansion","Acquired adjacent region "+operation.x+", "+operation.z,operation.id);
            sim.State.regions.Add(new RegionState{id=sim.State.Id("region"),x=operation.x,z=operation.z,owned=true,price=operation.quotedPrice});operation.active=false;sim.Notify(operation.id);
        }
        public void Tick(float dt)
        {
            if(sim.State.researchQueue.Count>0)
            {
                string id=sim.State.researchQueue[0];var research=sim.Catalog.Research(id);
                if(ResearchBlocker(id)==null&&research!=null)
                {
                    float amount=Mathf.Min(sim.State.researchPoints,research.cost-sim.State.researchProgress);sim.State.researchPoints-=amount;sim.State.researchProgress+=amount;
                    if(sim.State.researchProgress>=research.cost-.0001f)
                    {sim.State.completedResearch.Add(id);sim.State.researchQueue.RemoveAt(0);sim.State.researchProgress=0;if(id=="life")foreach(var b in sim.State.structures.Where(b=>sim.Definition(b).sealedModule))b.airCapacity*=1.25f;sim.Notify(id);}
                }
            }
            Weather(dt);Terraform(dt);Reputation(dt);
            sim.State.visitorTimer+=dt;if(sim.State.visitorTimer>sim.Day*Mathf.Lerp(3,1,sim.State.reputation/100))
            {sim.State.visitorTimer=0;sim.Traffic.AdmitVisitors();}
            Milestone("first-workers",sim.Population>=6);Milestone("sustainable-food",sim.Population>=12&&sim.State.structures.Any(b=>b.definition=="greenhouse"&&sim.Operating(b)));
            Milestone("first-guests",sim.State.actors.Any(a=>a.kind==ActorKind.Visitor&&a.location==PersonLocation.Arrived));Milestone("open-air",sim.OutdoorsSafe);
            Milestone("planet-restored",sim.State.environment.atmosphereWork>=100&&sim.State.environment.climateWork>=80&&sim.State.environment.soilWork>=40);
            if(!sim.State.actors.Any(a=>a.health>0&&a.location!=PersonLocation.Dead&&a.location!=PersonLocation.Departed)&&sim.State.credits<sim.Catalog.balance.recruitCost+sim.Catalog.balance.flightFee)
            {sim.State.failed=true;sim.State.speed=0;sim.Notify("colony-failure");}
        }
        void Milestone(string id,bool condition){if(condition&&!sim.State.acknowledged.Contains(id)){sim.State.acknowledged.Add(id);sim.Notify("milestone:"+id);}}
        public (float visitors,float residents,float safety,float variety) ReputationFactors()
        {
            var guests=sim.State.actors.Where(a=>a.kind==ActorKind.Visitor&&(a.location==PersonLocation.Arrived||a.location==PersonLocation.Departed)).TakeLast(100).ToArray();
            var residents=sim.SurfaceActors.Where(a=>a.kind==ActorKind.Colonist).ToArray();var modules=sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&sim.Definition(b).sealedModule).ToArray();
            float visitor=guests.Length>0?guests.Average(a=>a.morale):20,resident=residents.Length>0?residents.Average(a=>a.morale):50;
            float safety=modules.Length>0?100*modules.Count(b=>sim.Operating(b))/(float)modules.Length:60;
            float variety=sim.State.structures.Where(b=>(b.definition=="attraction"||b.definition=="garden"||b.definition=="park")&&sim.Operating(b)).Select(b=>b.definition).Distinct().Count()*100f/3;
            return(visitor,resident,safety,variety);
        }
        void Reputation(float dt){var r=ReputationFactors();float score=r.visitors*.5f+r.residents*.2f+r.safety*.2f+r.variety*.1f;sim.State.reputation=Mathf.Clamp(Mathf.MoveTowards(sim.State.reputation,score,dt/sim.Day*4),0,100);}
        void Weather(float dt)
        {
            var env=sim.State.environment;
            if(sim.State.time>=env.nextEvent)
            {
                string[] kinds={"Approaching storm","Equipment fault","Greenhouse illness","Medical demand","Delayed freight","Visitor surge"};string kind=kinds[Mathf.FloorToInt(sim.State.Random()*kinds.Length)];
                var targets=sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&(kind!="Greenhouse illness"||b.definition=="greenhouse")).ToArray();
                sim.State.events.Add(new EventState{id=sim.State.Id("event"),kind=kind,target=targets.Length>0?targets[Mathf.FloorToInt(sim.State.Random()*targets.Length)].id:null,begins=sim.State.time+30,ends=sim.State.time+30+sim.Day*(.5f+sim.State.Random()),intensity=.3f+sim.State.Random()*.45f});
                env.nextEvent=sim.State.time+sim.Day*(4+sim.State.Random()*4);env.wind=.3f+sim.State.Random()*.6f;
            }
            env.storm=0;
            foreach(var e in sim.State.events.Where(e=>e.begins<=sim.State.time&&e.ends>sim.State.time))
            {
                if(e.kind=="Approaching storm")env.storm=Mathf.Max(env.storm,e.intensity);
                if(e.applied)continue;e.applied=true;
                if(e.kind=="Equipment fault"){var b=sim.Structure(e.target);if(b!=null){b.condition=Mathf.Max(.2f,b.condition-.28f);sim.Networks.Dirty();}}
                if(e.kind=="Medical demand"){var people=sim.SurfaceActors.Where(a=>a.kind==ActorKind.Colonist).ToArray();if(people.Length>0){var patient=people[Mathf.FloorToInt(sim.State.Random()*people.Length)];patient.health=Mathf.Max(40,patient.health-25);}}
                if(e.kind=="Delayed freight")foreach(var f in sim.State.flights.Where(f=>f.kind==FlightKind.Import&&f.phase==FlightPhase.Transit))f.duration+=sim.Day*.5f;
                if(e.kind=="Visitor surge")sim.State.visitorTimer=sim.Day*4;
            }
            if(sim.State.events.Count>100)sim.State.events.RemoveAll(e=>e.ends<sim.State.time-sim.Year*2);
        }
        void Terraform(float dt)
        {
            var env=sim.State.environment;if(!env.programEnabled){env.blocker="Program paused";return;}
            if(!sim.State.completedResearch.Contains("planetary")){env.blocker="Planetary Engineering research required";return;}
            bool complete=env.atmosphereWork>=100&&env.climateWork>=80&&env.soilWork>=40;if(complete){env.blocker="Restoration complete · colony development continues";return;}
            var atmosphere=sim.State.structures.Where(b=>b.definition=="atmosphere"&&sim.Operating(b)).ToArray();var climate=sim.State.structures.Where(b=>b.definition=="climate"&&sim.Operating(b)).ToArray();var biosphere=sim.State.structures.Where(b=>b.definition=="biosphere"&&sim.Operating(b)).ToArray();
            if(atmosphere.Length+climate.Length+biosphere.Length==0){env.blocker="No powered planetary facilities";return;}
            if(env.programYears>=env.fundedYears)
            {if(sim.State.credits<sim.Catalog.balance.terraformFunding){env.blocker="Awaiting 2,000 credits for the next program year";return;}sim.Credit(-sim.Catalog.balance.terraformFunding,"Planetary funding","Planetary program year "+(Mathf.FloorToInt(env.fundedYears)+1));env.fundedYears+=1;}
            env.programYears+=dt/sim.Year;float supported=0;
            float Rate(int plants)=>Mathf.Min(sim.Catalog.balance.terraformCap,sim.Catalog.balance.terraformPerYear*plants*(sim.State.completedResearch.Contains("biosphere")?1.15f:1));
            int a=0,c=0,s=0;
            if(env.atmosphereWork<100)foreach(var b in atmosphere){float water=20*dt/sim.Day;if(sim.Networks.WaterAvailable(b.id)+.00001f>=water){sim.Networks.DrawWater(b.id,water);a++;}else b.blocker="Planetary plant needs 20 water/day";}
            if(env.climateWork<80)foreach(var b in climate){if(sim.Stock.Consume(sim.Stock.Get(b.inventory),Good.Minerals,4*dt/sim.Day))c++;else b.blocker="Climate array needs 4 minerals/day";}
            if(env.soilWork<40)foreach(var b in biosphere){float water=8*dt/sim.Day,bio=6*dt/sim.Day;if(sim.Stock.Available(sim.Stock.Get(b.inventory),Good.Biomass)>=bio&&sim.Networks.WaterAvailable(b.id)+.00001f>=water){sim.Stock.Consume(sim.Stock.Get(b.inventory),Good.Biomass,bio);sim.Networks.DrawWater(b.id,water);s++;}else b.blocker="Biosphere processor needs biomass and water";}
            env.atmosphereWork=Mathf.Min(100,env.atmosphereWork+Rate(a)*dt/sim.Year);env.climateWork=Mathf.Min(80,env.climateWork+Rate(c)*dt/sim.Year);env.soilWork=Mathf.Min(40,env.soilWork+Rate(s)*dt/sim.Year);
            env.atmosphere=Mathf.Lerp(env.atmosphereStart,100,env.atmosphereWork/100);env.climate=Mathf.Lerp(env.climateStart,100,env.climateWork/80);env.soil=Mathf.Lerp(env.soilStart,100,env.soilWork/40);
            supported=(a>0||env.atmosphereWork>=100?1:0)+(c>0||env.climateWork>=80?1:0)+(s>0||env.soilWork>=40?1:0);env.uptime=Mathf.Lerp(env.uptime,supported/3,dt/sim.Year);
            env.blocker=supported>=3?"All required branches supported; nominal longest branch ≈71 years":string.Join(" · ",new[]{a==0&&env.atmosphereWork<100?"Atmosphere support missing":null,c==0&&env.climateWork<80?"Climate support missing":null,s==0&&env.soilWork<40?"Biosphere support missing":null}.Where(x=>x!=null));
        }
    }
}
