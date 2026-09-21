using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Meridian.Colony
{
    public sealed partial class ColonyUI
    {
        string category="Housing",tradeQuantity="50",slotName="Colony-1";Good tradeGood=Good.Food;Profession recruitProfession=Profession.Builder;
        readonly HashSet<string> manifest=new HashSet<string>();List<SaveListing> saves=new List<SaveListing>();int peoplePage,machinePage,orbitPage;float displayDeadline;int oldWidth,oldHeight,oldMode;
        void HUD()
        {
            var state=sim.State;GUI.Box(new Rect(0,0,Width,85),GUIContent.none,skin.window);GUI.Box(new Rect(0,Height-70,Width,70),GUIContent.none,skin.window);
            GUI.Label(new Rect(24,12,340,32),state.name.ToUpperInvariant(),title);
            int day=Mathf.FloorToInt((float)(state.time/sim.Day));GUI.Label(new Rect(25,48,330,28),"YEAR "+(1+day/12)+"  /  DAY "+(day%12+1)+"   ·   "+(state.speed==0?"PAUSED":state.speed+"×"),muted);
            GUI.Label(new Rect(385,13,400,28),state.credits.ToString("N0")+" CR   ·   "+sim.Population+" RESIDENTS   ·   REP "+state.reputation.ToString("0"),accent);
            var totals=state.inventories.Where(i=>sim.Structure(i.owner)!=null).SelectMany(i=>i.items).GroupBy(i=>i.good).ToDictionary(g=>g.Key,g=>g.Sum(i=>i.quantity));
            string count(Good good)=>totals.TryGetValue(good,out float value)?value.ToString("N0"):"0";
            GUI.Label(new Rect(385,49,Width-920,28),"Metal "+count(Good.Metal)+"   Components "+count(Good.Components)+"   Food "+count(Good.Food)+"   Water "+count(Good.Water),small);
            float x=Width-470;foreach(var item in new[]{("Ⅱ",0f),("1×",1f),("2×",2f),("4×",4f)}){if(GUI.Button(new Rect(x,15,56,32),item.Item1))runtime.Speed(item.Item2);x+=60;}
            if(GUI.Button(new Rect(Width-225,15,100,32),state.masterFrames?"Full domes":"Frames"))runtime.Visuals.ToggleMaster();if(GUI.Button(new Rect(Width-117,15,94,32),"Menu")){Open("Pause");runtime.Speed(0);}
            GUI.Label(new Rect(Width-470,52,445,24),"Power "+sim.Networks.PowerSupply.ToString("0")+" / "+sim.Networks.PowerDemand.ToString("0")+" kW   ·   Air support "+sim.Networks.AirSupport.ToString("0"),small);
            string[] panels={"Overview","Build","Orbit","People","Machines","Storage","Utilities","Research","Trade","Visitors","Finance","Planetary","Regions","Guide"};float width=(Width-30)/panels.Length;
            for(int i=0;i<panels.Length;i++)if(GUI.Button(new Rect(15+i*width,Height-57,width-5,39),panels[i]))Open(panels[i]);
            if(runtime.BuildType!=null)GUI.Label(new Rect(30,Height-109,Width-60,35),sim.Catalog.Building(runtime.BuildType).name+"  ·  "+(runtime.PlacementReason??"Click to designate")+"  ·  R / Shift+R rotate  ·  Esc cancel",accent);
            else if(runtime.HarvestMode)GUI.Label(new Rect(30,Height-109,Width-60,35),"HARVEST  ·  Click or drag to designate resources  ·  Esc cancel",accent);
        }
        void Panel(string name)
        {
            switch(name)
            {
                case "Overview":Overview();break;case "Build":Build();break;case "Orbit":Orbit();break;case "People":People();break;case "Machines":Machines();break;
                case "Storage":Storage();break;case "Utilities":Utilities();break;case "Research":Research();break;case "Trade":Trade();break;case "Visitors":Visitors();break;
                case "Finance":Finance();break;case "Planetary":Planetary();break;case "Regions":Regions();break;case "Guide":Guide();break;case "Save":SaveBrowser(true);break;case "Load":SaveBrowser(false);break;case "Settings":Settings();break;case "Pause":Pause();break;
                case "Recovery":GUILayout.Label("EXPEDITION LOST",title);GUILayout.Label("No surviving expedition people or machines remain, and the balance cannot fund recruitment and transport. Load an earlier colony or return to the main menu to start a new expedition.");Button("Load an earlier save",()=>Open("Load"));Button("Return to main menu",()=>AskExit(runtime.ReturnToMenu));break;
            }
        }
        void Overview()
        {
            GUILayout.Label("EXPEDITION CONTROL",title);sim.State.name=Text("colony-name",sim.State.name);Row("Population",sim.Population+" resident colonists");Row("In orbit",sim.State.actors.Count(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Sleeping)+" sleeping passengers");
            Row("Calendar","120 seconds / day · 12 days / year");Row("Territory",(sim.State.regions.Count(r=>r.owned)*36)+" km²");sim.State.preferHumanBuilders=GUILayout.Toggle(sim.State.preferHumanBuilders,"Prefer colonists for eligible civilian work");
            Button("Designate harvest  [H]",runtime.BeginHarvest);Header("Alerts · select to inspect");
            Header("Work / rest policy");sim.State.workPolicy=GUILayout.SelectionGrid(sim.State.workPolicy,new[]{"Balanced","Earlier rest","Longer shifts"},3);GUILayout.Label("Longer shifts delay rest; accumulated fatigue still reduces morale and stops unsafe work.",muted);
            var danger=sim.SurfaceActors.Where(a=>(a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor)&&(a.health<60||a.hunger>70||a.thirst>70)||a.charge<5).Take(12).ToArray();
            foreach(var a in danger)Button(a.name+" · "+(a.reason==""?"Needs attention":a.reason),()=>{runtime.Focus(a.id);Inspect(a.id);});
            foreach(var b in sim.State.structures.Where(b=>b.phase!=BuildPhase.Removed&&(b.condition<.3f||sim.Definition(b).sealedModule&&b.phase==BuildPhase.Complete&&b.air<.3f)).Take(8))Button(b.name+" · "+b.blocker,()=>{runtime.Focus(b.id);Inspect(b.id);});
            foreach(var e in sim.State.events.Where(e=>e.ends>sim.State.time))GUILayout.Label(e.kind+" · "+(e.begins>sim.State.time?"in "+Mathf.CeilToInt((float)(e.begins-sim.State.time))+"s":"active"),warning);
            foreach(var f in sim.State.flights.Where(f=>f.phase==FlightPhase.Holding))Button(f.name+" · "+f.blocker,()=>Inspect(f.id));
            Header("Waiting work");foreach(var group in sim.State.jobs.Where(j=>sim.Jobs.Active(j)&&j.actor==null).GroupBy(j=>j.blocker).Take(8))GUILayout.Label(group.Count()+" · "+group.Key,muted);
        }
        void Build()
        {
            GUILayout.Label("CONSTRUCTION",title);GUILayout.Label("Designate foundations, then connect power, pipes and sealed corridors separately. Bots clear and deliver before building.",muted);
            string[] categories=catalog.buildings.Where(b=>b.category!="Starting"&&b.category!="Recovery").Select(b=>b.category).Distinct().ToArray();int index=Array.IndexOf(categories,category);int next=GUILayout.SelectionGrid(Mathf.Max(0,index),categories,3);category=categories[next];
            foreach(var d in catalog.buildings.Where(b=>b.category==category))
            {
                GUILayout.BeginVertical(card);GUILayout.Label(d.name,accent);GUILayout.Label(d.description,small);GUILayout.Label(string.Join(" · ",d.cost.Select(c=>c.quantity+" "+c.good))+(d.link?" / 10 m":""),muted);
                string research=string.IsNullOrEmpty(d.research)||sim.State.completedResearch.Contains(d.research)?null:catalog.Research(d.research).name;
                GUILayout.Label((d.machineOnly?"Machine labor":"Robots + eligible builders")+" · "+d.power.ToString("0.#")+" kW",small);
                Button(research==null?"Place "+d.name:"Requires "+research,()=>runtime.BeginBuild(d.id),research==null);GUILayout.EndVertical();
            }
        }
        void Orbit()
        {
            GUILayout.Label("ORBITAL ROSTER",title);GUILayout.Label("48 expedition colonists begin in independently supported cryosleep. Select up to six for a flight.",muted);
            var sleeping=sim.State.actors.Where(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Sleeping).ToArray();Pages(ref orbitPage,sleeping.Length);
            foreach(var a in sleeping.Skip(orbitPage*12).Take(12))
            {
                GUILayout.BeginHorizontal();bool selected=manifest.Contains(a.id);bool value=GUILayout.Toggle(selected,a.name+" · "+a.profession+" · "+a.trait);if(value&&!selected&&manifest.Count<6)manifest.Add(a.id);if(!value)manifest.Remove(a.id);if(GUILayout.Button("Details",GUILayout.Width(75)))Inspect(a.id);GUILayout.EndHorizontal();
            }
            manifest.RemoveWhere(id=>sim.Actor(id)?.location!=PersonLocation.Sleeping);Header("Flight review");Row("Selected",manifest.Count+" / 6");Row("Available beds",sim.People.FreeBeds(false).Count.ToString());Row("Transport","100 credits · 1 colony day");string reason=sim.Traffic.PersonnelWarning(manifest);
            if(reason!=null)GUILayout.Label(reason,warning);float food=sim.State.inventories.Sum(i=>sim.Stock.Available(i,Good.Food));if(food<(sim.Population+manifest.Count)*6)GUILayout.Label("Low food reserve: fewer than three days for the planned population.",warning);
            Button("Request personnel dropship",()=>Confirm("Launch this manifest?",string.Join(", ",manifest.Select(id=>sim.Actor(id).name))+"\n100 credits. Reserved beds remain assigned through the trip. Check the food and life-support figures above.",()=>{sim.Traffic.RequestPersonnel(manifest);manifest.Clear();}),reason==null);
            Header("Recruit to orbit");recruitProfession=(Profession)GUILayout.SelectionGrid((int)recruitProfession,Enum.GetNames(typeof(Profession)),3);Button("Recruit "+recruitProfession+" · 300 credits",()=>sim.Traffic.Recruit(recruitProfession),sim.State.credits>=300);GUILayout.Label("Recruit arrives in orbit after two days; a separate surface flight is still required.",muted);
            foreach(var r in sim.State.recruitment.Where(r=>!r.completed))Row(r.profession.ToString(),(r.remaining/sim.Day).ToString("0.0")+" days to orbit");Header("Space traffic");foreach(var f in sim.State.flights.Where(sim.Traffic.Active))Button(f.name+" · "+f.phase,()=>Inspect(f.id));
        }
        void Pages(ref int page,int count)
        {int last=Mathf.Max(0,(count-1)/12);page=Mathf.Clamp(page,0,last);GUILayout.BeginHorizontal();if(GUILayout.Button("Previous"))page=Mathf.Max(0,page-1);GUILayout.Label((page+1)+" / "+(last+1),small);if(GUILayout.Button("Next"))page=Mathf.Min(last,page+1);GUILayout.EndHorizontal();}
        void People()
        {
            var people=sim.State.actors.Where(a=>a.kind==ActorKind.Colonist||a.kind==ActorKind.Visitor).ToArray();GUILayout.Label("PEOPLE",title);Pages(ref peoplePage,people.Length);
            foreach(var a in people.Skip(peoplePage*12).Take(12))Button(a.name+" · "+(a.kind==ActorKind.Visitor?"Visitor":a.profession.ToString())+"\n"+a.location+" · "+a.activity,()=>Inspect(a.id));
        }
        void Machines()
        {var machines=sim.State.actors.Where(a=>a.kind!=ActorKind.Colonist&&a.kind!=ActorKind.Visitor).ToArray();GUILayout.Label("MACHINES",title);Pages(ref machinePage,machines.Length);foreach(var a in machines.Skip(machinePage*12).Take(12))Button(a.name+" · charge "+a.charge.ToString("0")+"%\n"+a.activity,()=>Inspect(a.id));}
        void Storage()
        {
            GUILayout.Label("STOCK & PRODUCTION",title);foreach(var good in catalog.goods)
            {
                float total=sim.State.inventories.Sum(i=>sim.Stock.Count(i,good.id)),reserved=sim.State.reservations.Where(r=>!r.incoming&&r.good==good.id).Sum(r=>r.quantity),transit=sim.State.actors.Sum(a=>sim.Stock.Count(sim.Stock.Get(a.inventory),good.id));
                Row(good.name,total.ToString("N1")+" owned · "+reserved.ToString("N1")+" reserved · "+transit.ToString("N1")+" carried");
            }
            Header("Storage filters and production controls");foreach(var b in sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&(sim.Jobs.Storage(b)||catalog.Recipe(b.recipe)!=null||b.deposit!=null)))Button(b.name+" · "+b.blocker,()=>Inspect(b.id));
        }
        void Utilities()
        {
            GUILayout.Label("UTILITY NETWORKS",title);Row("Generation",sim.Networks.PowerSupply.ToString("0.0")+" kW");Row("Demand",sim.Networks.PowerDemand.ToString("0.0")+" kW");Row("Stored energy",sim.State.structures.Sum(b=>b.battery).ToString("0.0")+" kWh");Row("Air production",sim.Networks.AirSupport.ToString("0")+" people-equivalent/day");
            GUILayout.Label("Cable carries electricity. Pipe carries finite stored water. Corridors join sealed air reserves and human walking routes. Connection types do not substitute for one another.",muted);
            foreach(string type in new[]{"cable","pipe","corridor"})Button("Place "+type,()=>runtime.BeginBuild(type));
            foreach(var b in sim.State.structures.Where(b=>b.phase==BuildPhase.Complete&&(sim.Definition(b).generation>0||sim.Definition(b).sealedModule||b.definition=="air"||b.definition=="well"||b.definition=="tank")))Button(b.name+" · "+b.powerFraction.ToString("P0")+" power · "+b.blocker,()=>Inspect(b.id));
        }
        void Research()
        {
            GUILayout.Label("RESEARCH",title);Row("Available points",sim.State.researchPoints.ToString("0.0"));GUILayout.Label("Two effectively staffed scientists produce ten points per day. Attendance includes a normal work/rest cycle.",muted);
            foreach(string id in sim.State.researchQueue.ToArray()){var r=catalog.Research(id);Bar(r.name,sim.State.researchQueue[0]==id?sim.State.researchProgress:0,r.cost);Button("Remove from queue",()=>sim.Development.RemoveResearch(id));}
            foreach(var r in catalog.research)
            {
                GUILayout.BeginVertical(card);GUILayout.Label(r.name+" · "+r.cost+" points",accent);GUILayout.Label(r.description,small);string blocker=sim.Development.ResearchBlocker(r.id);Button(sim.State.completedResearch.Contains(r.id)?"Completed":sim.State.researchQueue.Contains(r.id)?"Queued":blocker??"Add to research queue",()=>sim.Development.QueueResearch(r.id),blocker==null&&!sim.State.researchQueue.Contains(r.id));GUILayout.EndVertical();
            }
        }
        void Trade()
        {
            GUILayout.Label("GALACTIC EXCHANGE",title);GUILayout.Label("200 units per freighter · nominal three-day trip. Imports are paid once; exports are paid after reserved cargo physically loads and departs.",muted);
            tradeGood=(Good)GUILayout.SelectionGrid((int)tradeGood,catalog.goods.Select(g=>g.name).ToArray(),3);tradeQuantity=Text("trade-quantity",tradeQuantity);float.TryParse(tradeQuantity,out float quantity);quantity=Mathf.Floor(quantity);float buy=sim.Traffic.Quote(tradeGood,quantity,true),sell=sim.Traffic.Quote(tradeGood,quantity,false);
            Row("Buy / sell per unit",catalog.Good(tradeGood).buy+" / "+(catalog.Good(tradeGood).buy*.6f).ToString("0.0")+" credits");Row("Unreserved stock",sim.State.inventories.Where(i=>sim.Structure(i.owner)?.phase==BuildPhase.Complete).Sum(i=>sim.Stock.Available(i,tradeGood)).ToString("N0"));Row("Purchase total",buy.ToString("N0")+" credits");Row("Balance afterward",(sim.State.credits-buy).ToString("N0")+" credits");
            bool valid=quantity>=1&&quantity<=200;Button("Buy "+quantity+" "+tradeGood,()=>Confirm("Confirm import",quantity+" "+tradeGood+" for "+buy.ToString("N0")+" credits. Delivery uses your landing apron.",()=>sim.Traffic.Trade(tradeGood,quantity,true)),valid&&sim.State.credits>=buy);
            Button("Export for "+sell.ToString("N0")+" credits",()=>Confirm("Confirm export",quantity+" "+tradeGood+" will be reserved and hauled to the freight apron. Payment follows departure.",()=>sim.Traffic.Trade(tradeGood,quantity,false)),valid);
            foreach(var f in sim.State.flights.Where(f=>(f.kind==FlightKind.Import||f.kind==FlightKind.Export)&&sim.Traffic.Active(f)))Button(f.name+" · "+f.phase,()=>Inspect(f.id));
        }
        void Visitors()
        {
            GUILayout.Label("DESTINATION & VISITORS",title);sim.State.visitorsOpen=GUILayout.Toggle(sim.State.visitorsOpen,"Accept visitor groups");sim.State.visitorCap=Mathf.RoundToInt(GUILayout.HorizontalSlider(sim.State.visitorCap,2,100));Row("Visitor limit",sim.State.visitorCap.ToString());sim.State.pricePolicy=GUILayout.SelectionGrid(sim.State.pricePolicy,new[]{"Budget 0.75×","Standard","Premium 1.5×"},3);
            Row("Guest beds",sim.People.FreeBeds(true).Count+" available");Row("Guests on surface",sim.SurfaceActors.Count(a=>a.kind==ActorKind.Visitor).ToString());Bar("Reputation",sim.State.reputation);var r=sim.Development.ReputationFactors();Row("Visitor satisfaction",r.visitors.ToString("0")+" · weight 50%");Row("Resident wellbeing",r.residents.ToString("0")+" · weight 20%");Row("Safety / reliability",r.safety.ToString("0")+" · weight 20%");Row("Attraction variety",r.variety.ToString("0")+" · weight 10%");
            GUILayout.Label("Opening requires staffed reception and restaurant, available lodge beds, a staffed attraction or garden, and a safe sealed arrival route. Small groups can discover an operational destination at any reputation.",muted);
            foreach(var a in sim.SurfaceActors.Where(a=>a.kind==ActorKind.Visitor))Button(a.name+" · budget "+a.wallet.ToString("0")+" · "+Mathf.Max(0,(a.returnAfter-(float)sim.State.time)/sim.Day).ToString("0.0")+" days left",()=>Inspect(a.id));
        }
        void Finance()
        {
            GUILayout.Label("COLONY ACCOUNTS",title);Row("Balance",sim.State.credits.ToString("N0")+" credits");foreach(var group in sim.State.ledger.Where(l=>l.time>sim.State.time-sim.Year).GroupBy(l=>l.category))Row(group.Key,group.Sum(l=>l.amount).ToString("+0;-0;0"));Header("Recent transactions");foreach(var entry in sim.State.ledger.TakeLast(80).Reverse())GUILayout.Label("Day "+(1+(int)(entry.time/sim.Day))+" · "+entry.amount.ToString("+0;-0;0")+" · "+entry.description,small);
        }
        void Planetary()
        {
            var e=sim.State.environment;GUILayout.Label("PLANETARY RESTORATION",title);GUILayout.Label("Suitability scores describe habitability; they are not atmospheric gas concentrations.",muted);Bar("Atmosphere",e.atmosphere);Bar("Climate",e.climate);Bar("Soil & water",e.soil);
            Row("Exterior access",sim.OutdoorsSafe?"Approved without EVA":"Sealed access / builder EVA required");Row("Outdoor crops",sim.OutdoorsSafe&&e.soil>=80?"Approved":"Atmosphere, climate and soil must reach 80");Header("Coordinated program");sim.State.environment.programEnabled=GUILayout.Toggle(e.programEnabled,"Fund and operate planetary program");GUILayout.Label(e.blocker,warning);Bar("Atmospheric work",e.atmosphereWork);Bar("Climate work",e.climateWork,80);Bar("Soil work",e.soilWork,40);Row("Active program",e.programYears.ToString("0.00")+" years");Row("Funding","2,000 credits / program year");Row("Supported rate","1.4 work points/year; hard cap 2");Row("Recent support",e.uptime.ToString("P0"));
            float remaining=Mathf.Max((100-e.atmosphereWork)/1.4f,(80-e.climateWork)/1.4f,(40-e.soilWork)/1.4f);Row("Supported projection",remaining.ToString("0")+" more years nominal; longer with interruptions");GUILayout.Label("Atmosphere plants use 20 water/day; climate arrays 4 minerals/day; biosphere processors 6 biomass + 8 water/day. Power, condition and actual connected supplies determine progress.",muted);
        }
        void Regions()
        {
            GUILayout.Label("REGIONAL SURVEY",title);GUILayout.Label("Each adjacent region is 6 × 6 km. All regions share the original geographic frame and terrain; acquiring land does not add a new plateau.",muted);Row("Next acquisition",sim.Development.RegionPrice.ToString("N0")+" credits");
            if(sim.State.expansion.active){Bar("Survey",runtime.ExpansionProgress*100);GUILayout.Label(runtime.ExpansionStatus,muted);Button("Cancel acquisition",runtime.CancelExpansion);}
            var owned=sim.State.regions.Where(r=>r.owned).ToArray();var candidates=new HashSet<Vector2Int>();foreach(var r in owned)foreach(var d in new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down})candidates.Add(new Vector2Int(r.x,r.z)+d);
            var map=GUILayoutUtility.GetRect(10,210,GUILayout.ExpandWidth(true));int minX=owned.Min(r=>r.x)-1,maxX=owned.Max(r=>r.x)+1,minZ=owned.Min(r=>r.z)-1,maxZ=owned.Max(r=>r.z)+1;
            float cell=Mathf.Min(map.width/(maxX-minX+1),map.height/(maxZ-minZ+1));
            foreach(var point in candidates.Concat(owned.Select(r=>new Vector2Int(r.x,r.z))).Distinct())
            {
                var region=owned.FirstOrDefault(r=>r.x==point.x&&r.z==point.y);var rect=new Rect(map.x+(point.x-minX)*cell,map.y+(maxZ-point.y)*cell,cell-3,cell-3);var prior=GUI.backgroundColor;GUI.backgroundColor=region!=null?new Color(.35f,.8f,.65f):new Color(.9f,.65f,.3f);
                if(GUI.Button(rect,new GUIContent(point.x+", "+point.y,region!=null?"Owned territory · inspect":"Adjacent survey · select to review acquisition"))){if(region!=null)Inspect(region.id);else{int x=point.x,z=point.y;string reason=sim.Development.RegionBlocker(x,z);if(reason!=null)Toast(reason);else Confirm("Acquire region "+x+", "+z+"?",sim.Development.RegionPrice.ToString("N0")+" credits after successful survey; cancellation preserves your current colony.",()=>runtime.Expand(x,z));}}GUI.backgroundColor=prior;
            }
            Header("Owned territory");foreach(var r in owned)Button("Region "+r.x+", "+r.z+" · 36 km²",()=>runtime.Camera.Focus(sim.World.Ground(new Vector3(r.x*6000,0,r.z*6000))));
            Header("Available neighbors");foreach(var p in candidates.Where(p=>!owned.Any(r=>r.x==p.x&&r.z==p.y)).OrderBy(p=>p.y).ThenBy(p=>p.x))
            {string blocker=sim.Development.RegionBlocker(p.x,p.y);Button("Acquire "+p.x+", "+p.y+" · "+(blocker??sim.Development.RegionPrice.ToString("N0")+" credits"),()=>Confirm("Acquire adjacent region?","The survey is cancellable. Credits are charged only after all 36 terrain tiles are generated successfully.",()=>runtime.Expand(p.x,p.y)),blocker==null);}
        }
        void Guide()
        {
            GUILayout.Label("YOUR FIRST COLONY",title);GUILayout.Label("The machines can establish every essential facility before you wake a single person.",muted);
            var steps=new[]{("Land machines and starter cargo",sim.State.deployed),("Build power, a battery and charging capacity",sim.State.structures.Any(b=>b.definition=="solar"&&b.phase==BuildPhase.Complete)&&sim.State.structures.Any(b=>b.definition=="battery"&&b.phase==BuildPhase.Complete)),("Supply water and an air processor",sim.State.structures.Any(b=>b.definition=="well"&&sim.Operating(b))&&sim.State.structures.Any(b=>b.definition=="air"&&sim.Operating(b))),("Connect two habitats with sealed corridors",sim.People.FreeBeds(false).Count+sim.Population>=12),("Prepare canteen and two greenhouses",sim.State.structures.Count(b=>b.definition=="greenhouse"&&b.phase==BuildPhase.Complete)>=2&&sim.State.structures.Any(b=>b.definition=="canteen"&&b.phase==BuildPhase.Complete)),("Bring six colonists down from orbit",sim.Population>=6),("Grow to twelve people, including three botanists",sim.Population>=12&&sim.SurfaceActors.Count(a=>a.profession==Profession.Botanist&&a.kind==ActorKind.Colonist)>=3),("Develop science, visitors and neighboring land",sim.State.completedResearch.Count>=3)};
            foreach(var step in steps)GUILayout.Label((step.Item2?"✓  ":"○  ")+step.Item1,step.Item2?accent:skin.label);
            Header("Connection order");GUILayout.Label("Place facilities near the ship. Cable links electrical ports; pipe connects well/tanks and consumers. Corridors connect habitats/services to the apron and air processor. All three are separate tools in Build → Connections. Avoid blocking doors. The ship supplies 30 kW and two charging spots.");
            Header("Initial crew");GUILayout.Label("A useful twelve-person start includes 3 botanists, 2 scientists, 1 medic, 2 service workers, 1 technician and 3 builders. Two greenhouses need biomass and piped water. Food and water in personal packs are real stock, so meal and tap access matter.");
            Header("Controls");GUILayout.Label("WASD / arrows or middle-drag: screen-relative pan\nWheel: tilt · Shift + wheel / + / −: zoom\nQ / E: smooth 45° turns · Home: reset\nF: focus selected · B: build · H: harvest\nR / Shift+R: rotate placement\nSpace: pause · 1 / 2 / 3: time speed\nF5: quicksave · F9: quickload\nEsc: dismiss tool/window, then pause menu");
            Button("Dismiss guide",()=>{sim.State.guideDismissed=true;var w=Windows.Find(w=>w.key=="@Guide");if(w!=null)w.open=false;});
        }
        void Pause()
        {
            GUILayout.Label("COLONY MENU",title);Button("Resume",()=>{runtime.Speed(sim.State.previousSpeed>0?sim.State.previousSpeed:1);Windows.Find(w=>w.key=="@Pause").open=false;});Button("Save colony",()=>Open("Save"));Button("Load colony",()=>Open("Load"));Button("Settings",()=>Open("Settings"));Button("Return to main menu",()=>AskExit(runtime.ReturnToMenu));Button("Quit Meridian",()=>AskExit(Quit));
        }
        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying=false;
#else
            Application.Quit();
#endif
        }
        public void RefreshSaves(){if(catalog!=null)saves=ColonySaves.List(catalog);}
        public bool HasSave=>saves.Any(s=>s.Valid);
        public void ContinueLatest(){RefreshSaves();var latest=saves.FirstOrDefault(s=>s.Valid);if(latest==null)Toast("No compatible playable saves are available");else Load(latest);}
        void Load(SaveListing item){ColonyLoadPipeline.Load(item.path,transition,Toast);}
        void SaveBrowser(bool saving)
        {
            GUILayout.Label(saving?"SAVE COLONY":"LOAD COLONY",title);GUILayout.Label(ColonySaves.DirectoryPath,muted);if((runtime?runtime.Saving:ColonySaves.Writing))GUILayout.Label("Writing snapshot…",accent);if(ColonySaves.LastError!=null)GUILayout.Label(ColonySaves.LastError,warning);
            if(saving&&runtime){slotName=Text("slot-name",slotName);Button("Save named slot",()=>{string slot=slotName.Trim().Replace(' ','_');if(saves.Any(s=>s.slot==slot))Confirm("Overwrite save?","Replace "+slot+"? Its previous valid version will remain as a backup.",()=>runtime.Save(slot));else runtime.Save(slot);},!(runtime?runtime.Saving:ColonySaves.Writing));}
            Button("Refresh list",RefreshSaves);
            foreach(var item in saves.ToArray())
            {
                GUILayout.BeginVertical(card);GUILayout.Label(item.slot+(item.backup?" · BACKUP":""),accent);
                if(item.Valid)
                {
                    var metadata=item.metadata;GUILayout.Label(metadata.colony+" · Year "+metadata.year.ToString("0.0")+" · "+metadata.population+" residents",small);GUILayout.Label(metadata.utc,muted);
                    if(metadata.preview!=null)
                    {if(!previews.TryGetValue(item.path,out var image)){image=new Texture2D(2,2);try{image.LoadImage(Convert.FromBase64String(metadata.preview));previews[item.path]=image;}catch{Destroy(image);image=null;}}if(image)GUILayout.Label(image,GUILayout.Height(100));}
                    Button(item.backup?"Recover this backup":"Load this colony",()=>{if(runtime)Confirm("Load colony?","The current unsaved session will be replaced after the chosen save is validated and reconstructed.",()=>Load(item));else Load(item);});
                }
                else GUILayout.Label(item.error,warning);
                Button("Delete slot and backup",()=>Confirm("Delete this save slot?",item.slot+" and its backup will be permanently removed.",()=>{ColonySaves.Delete(item.slot);RefreshSaves();}),!(runtime?runtime.Saving:ColonySaves.Writing));GUILayout.EndVertical();
            }
            if(saves.Count==0)GUILayout.Label("No saves yet. Your first autosave is created when the expedition finishes landing.",muted);
        }
        void Settings()
        {
            Button("Reset window layout",ResetWindowLayout);
            var p=ColonySettings.Current;GUILayout.Label("SETTINGS",title);Header("Audio");p.master=Slider("Master volume",p.master,0,1);p.music=Slider("Music",p.music,0,1);p.effects=Slider("Effects",p.effects,0,1);p.ambience=Slider("Ambience",p.ambience,0,1);
            Header("Camera & interface");p.cameraSensitivity=Slider("Pan sensitivity",p.cameraSensitivity,.3f,2.5f);p.zoomSensitivity=Slider("Zoom sensitivity",p.zoomSensitivity,.3f,2.5f);p.tiltSensitivity=Slider("Tilt sensitivity",p.tiltSensitivity,.3f,2.5f);p.uiScale=Slider("Interface scale",p.uiScale,.75f,1.25f);p.edgePan=GUILayout.Toggle(p.edgePan,"Pan at screen edges");GUILayout.Label("Accessible alternatives: arrow keys pan; + / − zoom; on-screen pause, speed, build, harvest and save controls remain available without shortcuts.",muted);
            Header("Graphics");p.quality=GUILayout.SelectionGrid(Mathf.Clamp(p.quality,0,QualitySettings.names.Length-1),QualitySettings.names,2);p.mode=GUILayout.SelectionGrid(p.mode,new[]{"Exclusive","Borderless","Maximized","Windowed"},2);
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(3840,2160)})if(GUILayout.Toggle(p.width==size.x&&p.height==size.y,size.x+" × "+size.y,skin.button)){p.width=size.x;p.height=size.y;}
            Button("Apply display mode",()=>{oldWidth=Screen.width;oldHeight=Screen.height;oldMode=(int)Screen.fullScreenMode;ColonySettings.ApplyDisplay();displayDeadline=Time.unscaledTime+12;});Button("Save preferences",()=>{ColonySettings.Save();Toast("Preferences saved");});
        }
        float Slider(string label,float value,float min,float max){GUILayout.Label(label+"   "+value.ToString("0.00"),small);return GUILayout.HorizontalSlider(value,min,max);}
        void RevertDisplay(){displayDeadline=0;Screen.SetResolution(oldWidth,oldHeight,(FullScreenMode)oldMode);var p=ColonySettings.Current;p.width=oldWidth;p.height=oldHeight;p.mode=oldMode;}
    }
}
