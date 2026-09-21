using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Meridian.Colony
{
    [Serializable] public sealed class SaveEnvelope
    {
        public int format=1;public string slot,colony,utc,world,checksum,payload,preview;public float year;public int population;
    }
    public sealed class SaveListing {public string slot,path,error;public SaveEnvelope metadata;public bool backup;public bool Valid=>metadata!=null&&error==null;}
    public static class ColonySaves
    {
        static readonly object gate=new object();static Task writer=Task.CompletedTask;
        public static string DirectoryPath=>Path.Combine(Application.persistentDataPath,"Saves");
        public static bool Writing=>!writer.IsCompleted;
        public static string LastError{get;private set;}
        static string SafeSlot(string slot)
        {if(string.IsNullOrWhiteSpace(slot)||slot.Length>48||slot.Any(c=>!(char.IsLetterOrDigit(c)||c=='-'||c=='_')))throw new InvalidOperationException("Save names use letters, numbers, spaces converted to underscores, and hyphens (48 characters maximum)");return slot;}
        static string Hash(string value)
        {using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(Encoding.UTF8.GetBytes(value)));}
        public static Task Save(string slot,ColonyState state,ColonyCatalog catalog,byte[] preview=null,string directory=null)
        {
            // This method is called at the main-thread command boundary. The worker receives immutable strings/bytes.
            Validate(state,catalog);state.savedUtc=DateTime.UtcNow.ToString("O");string payload=JsonUtility.ToJson(state);
            var envelope=new SaveEnvelope{slot=SafeSlot(slot),colony=state.name,utc=state.savedUtc,world=state.worldId,payload=payload,checksum=Hash(payload),year=(float)(state.time/(catalog.balance.daySeconds*catalog.balance.daysPerYear))+1,population=state.actors.Count(a=>a.kind==ActorKind.Colonist&&a.location==PersonLocation.Arrived),preview=preview!=null?Convert.ToBase64String(preview):null};
            string contents=JsonUtility.ToJson(envelope),folder=directory??DirectoryPath;
            lock(gate)
            {
                writer=writer.ContinueWith(previous=>
                {
                    try
                    {
                        Directory.CreateDirectory(folder);string target=Path.Combine(folder,envelope.slot+".meridian"),temporary=target+"."+Guid.NewGuid().ToString("N")+".tmp";
                        try
                        {
                            byte[] bytes=Encoding.UTF8.GetBytes(contents);using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None,65536,FileOptions.WriteThrough)){stream.Write(bytes,0,bytes.Length);stream.Flush(true);}
                            if(File.ReadAllText(temporary)!=contents)throw new IOException("Save verification failed before publication");
                            if(File.Exists(target))File.Replace(temporary,target,target+".bak",true);else File.Move(temporary,target);LastError=null;
                        }
                        finally{if(File.Exists(temporary))File.Delete(temporary);}
                    }
                    catch(Exception error){LastError=error.Message;throw;}
                },TaskScheduler.Default);return writer;
            }
        }
        public static SaveEnvelope ReadEnvelope(string path)
        {
            var info=new FileInfo(path);if(info.Length>128*1024*1024)throw new InvalidDataException("Save exceeds the supported file size");
            var e=JsonUtility.FromJson<SaveEnvelope>(File.ReadAllText(path));
            if(e==null||e.format!=1||string.IsNullOrEmpty(e.payload)||Hash(e.payload)!=e.checksum)throw new InvalidDataException("Save is incomplete or its checksum does not match. Try its backup.");return e;
        }
        public static ColonyState Load(string path,ColonyCatalog catalog)
        {var envelope=ReadEnvelope(path);var state=Deserialize(envelope.payload);Validate(state,catalog);return state;}
        public static ColonyState Deserialize(string payload)
        {
            var state=JsonUtility.FromJson<ColonyState>(payload);if(state==null)return null;
            // Unity's native JSON reader can round a double timestamp by one ULP. Keep
            // clock/event/ledger boundaries bit-exact with the platform's round-trip reader.
            using(var document=System.Text.Json.JsonDocument.Parse(payload))
            {
                var root=document.RootElement;
                if(root.TryGetProperty("time",out var time))state.time=time.GetDouble();
                if(state.environment!=null&&root.TryGetProperty("environment",out var environment)&&environment.TryGetProperty("nextEvent",out var next))state.environment.nextEvent=next.GetDouble();
                if(state.events!=null&&root.TryGetProperty("events",out var events))for(int i=0;i<state.events.Count;i++){state.events[i].begins=events[i].GetProperty("begins").GetDouble();state.events[i].ends=events[i].GetProperty("ends").GetDouble();}
                if(state.ledger!=null&&root.TryGetProperty("ledger",out var ledger))for(int i=0;i<state.ledger.Count;i++)state.ledger[i].time=ledger[i].GetProperty("time").GetDouble();
            }
            return state;
        }
        public static List<SaveListing> List(ColonyCatalog catalog,string directory=null)
        {
            var result=new List<SaveListing>();string folder=directory??DirectoryPath;if(!Directory.Exists(folder))return result;
            foreach(string file in Directory.EnumerateFiles(folder,"*.meridian*"))
            {
                if(file.EndsWith(".tmp",StringComparison.OrdinalIgnoreCase)||!file.EndsWith(".meridian",StringComparison.OrdinalIgnoreCase)&&!file.EndsWith(".meridian.bak",StringComparison.OrdinalIgnoreCase))continue;
                var item=new SaveListing{slot=Path.GetFileName(file).Replace(".meridian.bak","").Replace(".meridian",""),path=file,backup=file.EndsWith(".bak")};
                try{item.metadata=ReadEnvelope(file);Validate(Deserialize(item.metadata.payload),catalog);item.metadata.payload=null;}
                catch(Exception error){item.error=error.Message;}result.Add(item);
            }
            return result.OrderByDescending(i=>i.metadata?.utc??"").ThenBy(i=>i.backup).ToList();
        }
        public static void Delete(string slot,string directory=null)
        {
            if(Writing)throw new InvalidOperationException("Wait for the current save to finish");string file=Path.Combine(directory??DirectoryPath,SafeSlot(slot)+".meridian");if(File.Exists(file))File.Delete(file);if(File.Exists(file+".bak"))File.Delete(file+".bak");
        }
        public static void Validate(ColonyState state,ColonyCatalog catalog)
        {
            if(state==null||state.schema!=ColonyState.CurrentSchema||state.contentVersion!="meridian-game-1")throw new InvalidDataException("Unsupported colony save version");
            ValidateShape(state,"colony",0);
            NormalizeReferences(state);
            if(state.setup==null||state.setup.planetVersion!=PlanetData.GeneratorVersion||state.setup.surfaceVersion!=SurfaceWorldData.GeneratorVersion)throw new InvalidDataException("The terrain generator version in this save is not supported; existing developed terrain will not be regenerated differently");
            if(state.setup.landing==null||!state.deployed||!state.setup.landingConfirmed||string.IsNullOrEmpty(state.worldId))throw new InvalidDataException("Save has no completed landing checkpoint");
            if(state.inventories==null||state.actors==null||state.structures==null||state.reservations==null||state.jobs==null||state.regions==null||state.flights==null||state.resources==null)throw new InvalidDataException("Save is missing required state tables");
            if(!Finite(state.credits)||state.credits<0||double.IsNaN(state.time)||double.IsInfinity(state.time)||state.time<0||state.nextId<1)throw new InvalidDataException("Invalid economy or calendar state");
            var ids=new HashSet<string>();void Identity(string id){if(string.IsNullOrEmpty(id)||!ids.Add(id))throw new InvalidDataException("Missing or repeated stable identity: "+id);}
            foreach(var b in state.structures){Identity(b.id);catalog.Building(b.definition);if(b.condition<0||b.condition>1.001f||b.progress<0||b.air<0||b.airCapacity<0||b.battery<0||b.foundation<0)throw new InvalidDataException("Invalid structure state");}
            foreach(var a in state.actors){Identity(a.id);if(a.health<0||a.health>100.01f||a.charge<0||a.charge>100.01f||a.wallet<0||a.pathIndex<0||a.pathIndex>a.path.Count)throw new InvalidDataException("Invalid person/machine state");}
            foreach(var inv in state.inventories){Identity(inv.id);if(inv.items==null||inv.slots<0||!Finite(inv.capacity)||inv.capacity<0)throw new InvalidDataException("Invalid inventory");foreach(var item in inv.items){Identity(item.id);if(!Finite(item.quantity)||item.quantity<=0||!Finite(item.condition)||catalog.Good(item.good)==null)throw new InvalidDataException("Invalid cargo item");}}
            foreach(var f in state.flights)Identity(f.id);foreach(var j in state.jobs)Identity(j.id);foreach(var r in state.reservations)Identity(r.id);
            var stock=new ColonyInventory(state,catalog);stock.Validate();
            var buildings=state.structures.ToDictionary(b=>b.id);var actors=state.actors.ToDictionary(a=>a.id);var jobs=state.jobs.ToDictionary(j=>j.id);
            foreach(var inv in state.inventories)if(!buildings.ContainsKey(inv.owner)&&!actors.ContainsKey(inv.owner)&&!state.flights.Any(f=>f.id==inv.owner))throw new InvalidDataException("Inventory owner is missing");
            foreach(var b in state.structures){if(stock.Get(b.inventory)==null||b.waterInventory!=null&&stock.Get(b.waterInventory)==null)throw new InvalidDataException("Structure storage is missing");if(catalog.Building(b.definition).link&&(b.from==null||b.to==null||!buildings.ContainsKey(b.from)||!buildings.ContainsKey(b.to)))throw new InvalidDataException("Network endpoint is missing");}
            foreach(var a in state.actors)
            {
                if(stock.Get(a.inventory)==null||a.equipment!=null&&stock.Get(a.equipment)==null)throw new InvalidDataException("Personal inventory is missing");
                if(a.home!=null&&!buildings.ContainsKey(a.home)||a.workplace!=null&&!buildings.ContainsKey(a.workplace)||a.building!=null&&!buildings.ContainsKey(a.building))throw new InvalidDataException("Person has a missing building reference");
                if(a.job!=null&&(!jobs.TryGetValue(a.job,out var job)||job.actor!=a.id))throw new InvalidDataException("Worker assignment is inconsistent");
            }
            foreach(var r in state.reservations)if(!jobs.ContainsKey(r.job)||stock.Get(r.inventory)==null||!Finite(r.quantity)||r.quantity<0)throw new InvalidDataException("Invalid logistics reservation");
            foreach(var j in state.jobs){if(j.actor!=null&&!actors.ContainsKey(j.actor))throw new InvalidDataException("Job worker is missing");if(j.source!=null&&stock.Get(j.source)==null||j.destination!=null&&stock.Get(j.destination)==null)throw new InvalidDataException("Job storage is missing");}
            var manifests=new HashSet<string>();foreach(var f in state.flights)
            {
                if(stock.Get(f.inventory)==null)throw new InvalidDataException("Flight cargo is missing");
                foreach(string id in f.passengers){if(!actors.ContainsKey(id))throw new InvalidDataException("Flight passenger is missing");if(f.kind!=FlightKind.Departure&&f.phase!=FlightPhase.Complete&&f.phase!=FlightPhase.Cancelled&&!manifests.Add(id))throw new InvalidDataException("Passenger is reserved on multiple flights");}
            }
            if(state.regions.Count(r=>r.owned&&r.x==0&&r.z==0)!=1||state.regions.GroupBy(r=>new{r.x,r.z}).Any(g=>g.Count()>1))throw new InvalidDataException("Invalid region ownership");
            if(state.resources.Select(r=>r.id).Distinct().Count()!=state.resources.Count||state.resources.Any(r=>!Finite(r.remaining)||r.remaining<0))throw new InvalidDataException("Invalid resource depletion data");
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static void ValidateShape(object value,string path,int depth)
        {
            if(value==null||depth>24)throw new InvalidDataException("Missing or invalid saved state: "+path);
            var type=value.GetType();
            if(value is float f){if(!Finite(f))throw new InvalidDataException("Non-finite saved value: "+path);return;}
            if(value is double d){if(double.IsNaN(d)||double.IsInfinity(d))throw new InvalidDataException("Non-finite saved value: "+path);return;}
            if(type.IsEnum){if(!Enum.IsDefined(type,value))throw new InvalidDataException("Unknown saved option: "+path);return;}
            if(type.IsPrimitive||value is string)return;
            if(value is System.Collections.IEnumerable sequence)
            {int i=0;foreach(var item in sequence){if(i++>2000000)throw new InvalidDataException("Saved table is too large: "+path);ValidateShape(item,path+"[]",depth+1);}return;}
            foreach(var field in type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
            {var child=field.GetValue(value);if(field.FieldType==typeof(string)&&child==null)continue;ValidateShape(child,path+"."+field.Name,depth+1);}
        }
        public static void NormalizeReferences(ColonyState state)
        {
            // Unity JSON writes null strings as empty strings. Normalize optional foreign keys at every reconstruction boundary.
            string Optional(string value)=>string.IsNullOrEmpty(value)?null:value;
            if(state.structures!=null)foreach(var b in state.structures){b.waterInventory=Optional(b.waterInventory);b.from=Optional(b.from);b.to=Optional(b.to);b.deposit=Optional(b.deposit);}
            if(state.actors!=null)foreach(var a in state.actors){a.trainingCenter=Optional(a.trainingCenter);a.equipment=Optional(a.equipment);a.home=Optional(a.home);a.workplace=Optional(a.workplace);a.job=Optional(a.job);a.airlock=Optional(a.airlock);a.building=Optional(a.building);a.intention=Optional(a.intention);a.target=Optional(a.target);}
            if(state.jobs!=null)foreach(var j in state.jobs){j.actor=Optional(j.actor);j.source=Optional(j.source);j.destination=Optional(j.destination);j.reservation=Optional(j.reservation);}
            if(state.flights!=null)foreach(var f in state.flights)f.pad=Optional(f.pad);
            if(state.resources!=null)foreach(var r in state.resources)r.claim=Optional(r.claim);
            state.selected=Optional(state.selected);
        }
    }
}
