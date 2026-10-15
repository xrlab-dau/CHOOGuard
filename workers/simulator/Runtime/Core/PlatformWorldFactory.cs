using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace ChooGuard.Seeded
{
    [Serializable] public sealed class SourcePoint { public double x, y, z; }
    [Serializable] public sealed class PlatformStrip { public string id, label; public SourcePoint a, b; public double halfWidth; }
    [Serializable] public sealed class SourceDoor
    {
        public string[] leaves; public string lift; public SourcePoint openDelta, liftDelta, outside, inside;
    }
    [Serializable] public sealed class SourceCar
    {
        public string name, label; public int number; public double floor; public bool reachable;
        public SourceDoor door; public SourcePoint[] aisle; public SourcePoint min, max;
    }
    [Serializable] public sealed class SourceTrain { public string root; public SourcePoint axis; public SourceCar[] cars; }
    [Serializable] public sealed class PlatformSource
    {
        public string source, source_sha256, claim; public SourceTrain train; public PlatformStrip[] platforms;
        public static PlatformSource Parse(string json)
        {
            var data = JsonConvert.DeserializeObject<PlatformSource>(json);
            if (data?.train?.cars == null || data.train.cars.Length==0 || data.platforms == null || data.platforms.Length==0 || data.train.axis == null)
                throw new ArgumentException("Platform source with train, cars and platform strips required");
            foreach (var p in data.platforms)
                if (!Valid(p.a) || !Valid(p.b) || !Finite(p.halfWidth) || p.halfWidth <= 0 || string.IsNullOrEmpty(p.id) || Math.Abs(p.a.x-p.b.x)+Math.Abs(p.a.z-p.b.z)<.01) throw new ArgumentException("Invalid source platform");
            foreach (var c in data.train.cars)
                if (c.door==null || !Valid(c.door.inside) || !Valid(c.door.outside) || !Valid(c.min) || !Valid(c.max) || !Finite(c.floor) || c.aisle == null || c.aisle.Length == 0 || c.aisle.Any(p=>!Valid(p))) throw new ArgumentException("Invalid source car");
            if(!Valid(data.train.axis)||Math.Abs(data.train.axis.x)+Math.Abs(data.train.axis.z)<.1)throw new ArgumentException("Invalid source train axis");
            return data;
        }
        static bool Finite(double x)=>!double.IsNaN(x)&&!double.IsInfinity(x);
        static bool Valid(SourcePoint p)=>p!=null&&Finite(p.x)&&Finite(p.y)&&Finite(p.z);
    }

    /// <summary>Coordinates derive from supplied model DTOs. Risk/health/material parameters are training assumptions.</summary>
    public static class PlatformWorldFactory
    {
        public static World Create(string json, ulong seed) => Create(json, seed, 32);
        public static World Create(string json, string manifestJson, ulong seed, int population=32)
        {
            var world=Create(json,seed,population);WorldCoverage.Apply(world,manifestJson);return world;
        }
        public static World Create(string json, ulong seed, int population)
        {
            if (population < 0 || population > 500) throw new ArgumentOutOfRangeException(nameof(population));
            var data = PlatformSource.Parse(json);
            var world = new World { Seed = seed };
            world.Fields["ambient.temperature"] = 12 + Stable.Unit(seed,"environment",0,"temperature") * 24;
            world.Fields["rain"] = Stable.Unit(seed,"environment",0,"rain");
            world.Fields["wind"] = Stable.Unit(seed,"environment",0,"wind") * 15;
            world.Fields["seismic"] = 0; world.Fields["regional-alert"] = 0; world.Fields["communications-disruption"] = 0;
            foreach (var p in data.platforms.OrderBy(x=>x.id,StringComparer.Ordinal))
            {
                var e = Add(world,"platform:"+p.id,"platform",(p.a.x+p.b.x)*.5,0,(p.a.z+p.b.z)*.5,"walkable","structural","exposed","training-model");
                e.Set("integrity",1); e.Set("friction",.7); e.Set("water",0); e.Set("width",p.halfWidth*2);
                double dx=p.b.x-p.a.x,dz=p.b.z-p.a.z,length=Math.Sqrt(dx*dx+dz*dz);
                e.Set("half.length",length*.5);e.Set("half.width",p.halfWidth);e.Set("axis.x",dx/length);e.Set("axis.z",dz/length);
                // Modeled end-of-platform assembly point; not a verified actual station exit.
                var exit = Add(world,"exit:"+p.id,"assembly-point",p.a.x,p.a.y,p.a.z,"exit","walkable","safe-zone","training-model");
                e.Links.Add(exit.Id);
            }
            var main = data.platforms.FirstOrDefault(x=>x.id=="p56") ?? data.platforms[0];
            var train = Add(world,"train:ktx","train",88.91879,-.73000026,76.442825,"train","moving-body","structural","electrical","thermal","training-model");
            train.Set("speed",0); train.Set("at-platform",1); train.Set("integrity",1); train.Set("temperature",24); train.Set("power",1); train.Set("hold",0);
            foreach (var c in data.train.cars.OrderBy(x=>x.number))
            {
                string carId="car:"+c.number;
                var car=Add(world,carId,"car",(c.min.x+c.max.x)*.5,c.floor,(c.min.z+c.max.z)*.5,"semantic","structural","combustible","sheltered","training-model");
                car.ParentId=train.Id; car.Set("integrity",1); car.Set("fuel",.5); car.Set("temperature",24); car.Set("smoke",0); car.Set("occupancy",0);
                var door=Add(world,"door:"+c.number,"door",c.door.outside.x,c.door.outside.y,c.door.outside.z,"semantic","door","electrical","training-model");
                door.ParentId=carId; door.Set("open",1); door.Set("jammed",0); door.Set("power",1); door.Set("integrity",1); door.Set("locked",0);
                door.Links.Add(carId); door.Links.Add("platform:"+main.id); car.Links.Add(door.Id); train.Links.Add(carId);
                var wait=Add(world,"wait:"+c.number,"platform-wait",c.door.outside.x-data.train.axis.z*2,0,c.door.outside.z+data.train.axis.x*2,"place","walkable","training-model");wait.Links.Add(door.Id);
            }
            var anchor=data.train.cars[1 % data.train.cars.Length].door.outside;
            var axis=data.train.axis; double sx=-axis.z,sz=axis.x;
            var power=Add(world,"power:platform","service-cabinet",anchor.x+sx*3,anchor.y,anchor.z+sz*3,"power-source","electrical","thermal","water-sensitive","self-powered","exposed","training-model");
            power.Set("power",1);power.Set("supply.available",1);power.Set("power.isolated",0);power.Set("load",.35+Stable.Unit(seed,"material",0,power.Id)*.5);power.Set("capacity",1);power.Set("temperature",24);power.Set("integrity",1);
            power.Set("wear",.05+Stable.Unit(seed,"material",0,power.Id,2)*.5);power.Set("insulation",.35+Stable.Unit(seed,"material",0,power.Id,3)*.6);power.Set("water.tolerance",.3+Stable.Unit(seed,"material",0,power.Id,4)*.6);
            foreach(var e in world.Entities.Values.Where(x=>x.Has("electrical") && x.Id!=power.Id))e.Links.Add(power.Id);
            var radio=Add(world,"radio:platform","public-address",anchor.x+sx*2,0,anchor.z+sz*2,"radio","alarm","electrical","training-model");radio.Set("power",1);radio.Set("alarm.active",0);radio.Links.Add(power.Id);
            var tool=Add(world,"extinguisher:platform","extinguisher",anchor.x+sx*2+axis.x*2,0,anchor.z+sz*2+axis.z*2,"extinguisher","training-model");tool.Set("charge",1);
            for(int i=0;i<population;i++)
            {
                string id="npc:"+i.ToString("D4");
                double along=(Stable.Unit(seed,"population",0,id,0)-.5)*100;
                double lateral=1.5+Stable.Unit(seed,"population",0,id,1)*1.5;
                var npc=Add(world,id,"person",anchor.x+axis.x*along+sx*lateral,0,anchor.z+axis.z*along+sz*lateral,"npc","training-model");
                npc.Set("health",1);npc.Set("stress",Stable.Unit(seed,"population",0,id,2)*.15);npc.Set("fatigue",Stable.Unit(seed,"population",0,id,3)*.2);
                npc.Set("mobility",.5+Stable.Unit(seed,"population",0,id,4)*.5);npc.Set("medical.vulnerability",Stable.Unit(seed,"population",0,id,5)*.03);
                npc.Set("mobility.base",npc.Get("mobility"));
                if(Stable.Unit(seed,"population",0,id,6)<.15)npc.Tags.Add("medical-vulnerable");
                npc.Links.Add("exit:"+main.id);foreach(var dest in world.Entities.Values.Where(x=>x.Has("place")))npc.Links.Add(dest.Id);
                npc.Set("goal.x",npc.X);npc.Set("goal.y",npc.Y);npc.Set("goal.z",npc.Z);npc.Set("goal.active",0);
                var bag=Add(world,"bag:"+i.ToString("D4"),"luggage",npc.X+.3,npc.Y,npc.Z,"semantic","combustible","obstacle","training-model");
                bag.ParentId=id;bag.Set("fuel",.1+Stable.Unit(seed,"material",0,bag.Id)*.4);bag.Set("temperature",world.Fields["ambient.temperature"]);bag.Set("integrity",1);bag.Set("mass",2+Stable.Unit(seed,"material",0,bag.Id,1)*18);
            }
            var staff=Add(world,"player","staff",anchor.x+sx*2,0,anchor.z+sz*2,"npc","staff","radio","training-model");staff.Set("health",1);staff.Set("mobility",1);
            return world;
        }
        static Entity Add(World world,string id,string kind,double x,double y,double z,params string[] tags)
        {
            var e=new Entity{Id=id,Kind=kind,X=x,Y=y,Z=z};foreach(var tag in tags)e.Tags.Add(tag);world.Add(e);return e;
        }
    }
}
