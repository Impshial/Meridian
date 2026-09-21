using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Meridian.Colony;

namespace Meridian.Editor
{
    public static partial class ColonyValidation
    {
        public static async void Safeguards()
        {
            const string log="Logs/ColonySafeguardValidation.txt";File.WriteAllText(log,"RUNNING");
            try
            {
                Catalog=ColonyCatalog.Load();var source=ColonySaves.Load("Library/GameValidation/connected-colony.meridian",Catalog);string snapshot=JsonUtility.ToJson(source);
                if(ColonyRuntime.Current){World=ColonyRuntime.Current.Simulation.World.Surface;Planet=SetupSession.Current.Planet;}
                await Task.Run(()=>
                {
                    if(World==null){Planet=PlanetGenerator.Generate(source.setup.seed,source.setup.planetParameters);Planet.ReleaseAppearanceBuffers();World=SurfaceGenerator.Generate(Planet,source.setup.region.localDirection,source.setup.surfaceParameters,default,null,0,source.setup);}
                    var sim=RestoreScenario(snapshot);var robot=sim.SurfaceActors.First(a=>a.kind==ActorKind.WorkRobot);var ship=sim.State.structures.First(b=>b.definition=="ship");
                    var remote=sim.AddStructure("solar",ship.position+new Vector3(5800,0,0),0,false);var work=new JobState{kind=JobKind.Build,target=remote.id};float charge=sim.Jobs.RequiredCharge(robot,work);Require(charge>30&&charge<100,"Remote work does not reserve journey and return energy");
                    sim=RestoreScenario(snapshot);var people=sim.State.actors.Where(a=>a.location==PersonLocation.Sleeping).Take(2).Select(a=>a.id).ToArray();var flight=sim.Traffic.RequestPersonnel(people);sim.Actor(people[0]).home="removed-room";flight.phase=FlightPhase.Holding;sim.Traffic.Tick(.1f);Require(flight.phase==FlightPhase.Descending&&sim.Actor(people[0]).home!="removed-room","Safe replacement bed not reserved");
                    sim=RestoreScenario(snapshot);ship=sim.State.structures.First(b=>b.definition=="ship");var farm=sim.AddStructure("field",sim.World.Ground(ship.position+new Vector3(-30,0,30)),0,true);StressLink(sim,"pipe",ship,farm);StressLink(sim,"cable",ship,farm);sim.Networks.Rebuild();sim.Networks.Tick(0);
                    var botanist=sim.SurfaceActors.First(a=>a.profession==Profession.Botanist);botanist.workplace=farm.id;sim.Stock.Add(sim.Stock.Get(farm.inventory),Good.Biomass,10);farm.powerFraction=1;farm.tending=60;
                    sim.Industry.Tick(1);Require(sim.Stock.Count(sim.Stock.Get(farm.inventory),Good.Food)==0,"Outdoor crops grew before environmental threshold");
                    sim.State.environment.atmosphere=sim.State.environment.climate=sim.State.environment.soil=100;float biomass=sim.Stock.Count(sim.Stock.Get(farm.inventory),Good.Biomass),water=sim.Networks.WaterAvailable(farm.id);
                    sim.Industry.Tick(1);Require(sim.Stock.Count(sim.Stock.Get(farm.inventory),Good.Food)>0&&sim.Stock.Count(sim.Stock.Get(farm.inventory),Good.Biomass)<biomass&&sim.Networks.WaterAvailable(farm.id)<water,"Safe outdoor farming did not consume irrigation/feedstock");
                    // Paid tending is authoritative; removing it must stop production even with staff assigned.
                    farm.tending=0;float food=sim.Stock.Count(sim.Stock.Get(farm.inventory),Good.Food);sim.Industry.Tick(1);Require(sim.Stock.Count(sim.Stock.Get(farm.inventory),Good.Food)==food,"Untended outdoor farm produced food");
                });
                File.WriteAllText(log,"PASS\nRemote machine work includes journey, work and return charge; invalid flight room is replaced by a safe reserved bed; outdoor farm enforces environment, tending, biomass and irrigation. Farm structures and tending buffer explicitly granted for branch checks.");
            }
            catch(Exception error){File.WriteAllText(log,"FAIL\n"+error);Debug.LogException(error);}
        }
    }
}
