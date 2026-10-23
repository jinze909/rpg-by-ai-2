using System;
namespace Starfall
{
    // Original procedural scores and soundscapes: no external music dependency.
    public static class AudioSynthesis
    {
        public const int Rate = 22050;
        public static readonly string[] Events = { "attack", "hit", "guard", "counter", "dash", "skill", "reward", "quest", "hurt", "travel", "ui", "deny", "equip", "save", "step", "windup", "slam", "arrow", "spell", "fishBite" };
        private static readonly int[][] Melodies = {
            new[] { 64, 67, 69, 67, 62, 64, 67, 62 }, new[] { 60, 63, 67, 63, 58, 60, 65, 63 },
            new[] { 62, 65, 69, 72, 69, 65, 64, 62 }, new[] { 57, 60, 64, 67, 64, 62, 60, 57 },
            new[] { 67, 69, 71, 74, 71, 69, 66, 67 }, new[] { 52, 55, 59, 58, 55, 53, 52, 47 },
            new[] { 57, 57, 60, 64, 62, 60, 59, 55 }
        };
        public static int Theme(int room, bool combat) { return combat ? 6 : room == 0 || room == 9 ? 0 : room == 5 ? 2 : room == 6 ? 3 : room == 7 ? 4 : room == 8 || room == 4 ? 5 : 1; }
        private static double Frequency(int note) { return 440 * Math.Pow(2, (note - 69) / 12.0); }
        public static float[] Music(int theme)
        {
            int[] notes = Melodies[Math.Max(0, Math.Min(6, theme))]; var wave = new float[Rate * 16];
            for (int i = 0; i < wave.Length; i++)
            {
                double time = i / (double)Rate, local = time % .5, f = Frequency(notes[(int)(time * 2) % 8]);
                double envelope = Math.Min(1, local * 35) * Math.Exp(-local * 5.8), bass = Frequency(notes[(int)(time / 4) * 2 % 8] - 24);
                double note = (Math.Sin(2 * Math.PI * f * time) + .25 * Math.Sin(4 * Math.PI * f * time)) * envelope * .16;
                double bed = Math.Sin(2 * Math.PI * bass * time) * .09 * Math.Pow(Math.Sin(Math.PI * (time % 4) / 4), 2);
                double pulse = theme == 6 ? Math.Sin(2 * Math.PI * 68 * local) * Math.Exp(-local * 28) * .12 : 0;
                double fade = Math.Min(1, Math.Min(time * 8, (16 - time) * 8)); wave[i] = (float)((note + bed + pulse) * fade);
            }
            return wave;
        }
        public static float[] Ambience(int room)
        {
            var wave = new float[Rate * 8]; var random = new Random(731 + room); double filtered = 0;
            for (int i = 0; i < wave.Length; i++)
            {
                double t = i / (double)Rate, noise = random.NextDouble() * 2 - 1; filtered = filtered * .97 + noise * .03;
                double swell = room == 5 || room == 2 ? .15 + .1 * Math.Sin(t * Math.PI / 2) : .09 + .04 * Math.Sin(t * Math.PI / 4);
                double bird = room < 2 && t > 2 && t < 2.3 ? Math.Sin(t * 2 * Math.PI * (1400 + 150 * Math.Sin(t * 35))) * Math.Sin((t - 2) / .3 * Math.PI) * .03 : 0;
                wave[i] = (float)((filtered * swell + bird) * Math.Min(1, Math.Min(t * 4, (8 - t) * 4)));
            }
            return wave;
        }
        public static float[] Event(string name)
        {
            if (name == "step") return Footstep("grass", 0);
            if (name == "fishBite") return FishBite();
            int id = Array.IndexOf(Events, name); if (id < 0) id = 0;
            double duration = name == "quest" || name == "save" ? .55 : name == "travel" || name == "skill" ? .4 : name == "windup" || name == "spell" ? .28 : name == "slam" ? .24 : .12;
            var wave = new float[(int)(Rate * duration)]; var random = new Random(id + 71); double phase = 0;
            for (int i = 0; i < wave.Length; i++)
            {
                double t = i / (double)wave.Length, start = name == "hurt" ? 130 : name == "hit" ? 210 : name == "guard" ? 320 : 300 + id * 37;
                if (name == "slam") start = 78; else if (name == "windup") start = 160; else if (name == "arrow") start = 950; else if (name == "spell") start = 540;
                double f = name == "quest" || name == "reward" || name == "save" ? start * new[] { 1.0, 1.25, 1.5 }[Math.Min(2, (int)(t * 3))] : start * (1 + (name == "hurt" || name == "deny" ? -.7 : .5) * t);
                if (name == "slam") f = start * (1 - t * .5); else if (name == "arrow") f = start * (1 - t * .6);
                phase += 2 * Math.PI * f / Rate; double noise = name == "hit" || name == "attack" || name == "step" ? (random.NextDouble() * 2 - 1) * .18 : 0;
                if (name == "slam" || name == "arrow") noise = (random.NextDouble() * 2 - 1) * .2;
                wave[i] = (float)((Math.Sin(phase) * .36 + noise) * (1 - t) * Math.Min(1, t * 30));
            }
            return wave;
        }
        // Distinctive, deterministic water plop: a descending air bubble
        // wrapped in filtered ripples. Synthesized at runtime, with no asset
        // import, scene changes or external audio dependencies.
        private static float[] FishBite()
        {
            float[] wave = new float[(int)(Rate * .24f)];
            var random = new Random(8123);
            double bubblePhase = 0, filteredWater = 0;
            for (int i = 0; i < wave.Length; i++)
            {
                double seconds = i / (double)Rate;
                double progress = i / (double)wave.Length;
                filteredWater += ((random.NextDouble() * 2 - 1) - filteredWater) * .095;
                double pitch = 710 - 440 * progress;
                bubblePhase += 2 * Math.PI * pitch / Rate;
                double envelope = Math.Min(1, seconds * 90) *
                    Math.Exp(-seconds * 17) * Math.Min(1, (.24 - seconds) * 90);
                wave[i] = (float)((Math.Sin(bubblePhase) * .34 + filteredWater * .22) * envelope);
            }
            return wave;
        }
        public static float[] Footstep(string surface, int variant)
        {
            // Soft, low-passed contact noise; no repeating high pitched beep.
            int material = surface == "stone" ? 1 : surface == "wood" ? 2 : surface == "sand" ? 3 : surface == "snow" ? 4 : 0;
            var wave = new float[(int)(Rate * .14f)]; var noise = new Random(9021 + material * 83 + variant * 13); double filtered = 0;
            for (int i = 0; i < wave.Length; i++)
            {
                double t = i / (double)Rate, smooth = material == 1 ? .19 : material == 2 ? .1 : .06;
                filtered += ((noise.NextDouble() * 2 - 1) - filtered) * smooth;
                double envelope = Math.Min(1, t * 160) * Math.Exp(-t * (material == 4 ? 25 : 36)) * Math.Min(1, (.14 - t) * 55);
                double weight = material == 1 || material == 2 ? .028 * Math.Sin(t * Math.PI * 2 * (material == 2 ? 112 : 78)) : 0;
                wave[i] = (float)((filtered * .12 + weight) * envelope);
            }
            return wave;
        }
    }
}
