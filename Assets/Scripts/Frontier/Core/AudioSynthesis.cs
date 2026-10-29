using System;
namespace Starfall
{
    // Original procedural scores and soundscapes: no external music dependency.
    public static class AudioSynthesis
    {
        public const int Rate = 22050;
        public static readonly string[] Events = { "attack", "hit", "guard", "counter", "dash", "skill", "reward", "quest", "hurt", "travel", "ui", "deny", "equip", "save", "step", "windup", "slam", "arrow", "spell", "fishBite", "stoneThrow", "stoneImpact", "ambush", "sentryAlarm", "wayfarerShout", "railDepart" };
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
                // Every half-second phrase must release its note before the next
                // phrase (including the 16-second loop seam). A short
                // per-note tail replaces the old full-mix fade-out that made
                // every looping Unity AudioSource audibly duck at 16 seconds.
                double envelope = Math.Min(1, local * 35) * Math.Exp(-local * 5.8) *
                    Math.Min(1, (.5 - local) * 38);
                double bass = Frequency(notes[(int)(time / 4) * 2 % 8] - 24);
                double note = (Math.Sin(2 * Math.PI * f * time) + .25 * Math.Sin(4 * Math.PI * f * time)) * envelope * .16;
                double bed = Math.Sin(2 * Math.PI * bass * time) * .09 * Math.Pow(Math.Sin(Math.PI * (time % 4) / 4), 2);
                double pulse = theme == 6 ? Math.Sin(2 * Math.PI * 68 * local) * Math.Exp(-local * 28) * .12 : 0;
                // The bass resolves at the four-second bar boundary and combat
                // percussion resolves at each half-second beat. Therefore
                // all three voices naturally meet at zero at the buffer seam
                // without a 125ms fade of the entire audible score.
                wave[i] = (float)(note + bed + pulse);
            }
            return wave;
        }
        public static float[] Ambience(int room)
        {
            // Atmospheres are looped by a REAL Unity AudioSource. Fading
            // every eight-second buffer to silence makes all wind, shore and
            // forest ambience audibly breathe in and out at each wrap.
            // Create half a second of filtered tail beyond the loop end,
            // then overlap-add that tail into the beginning of the loop.
            // The end and start now come from consecutive points of the
            // same deterministic noise stream without an artificial dip.
            const int seconds = 8;
            int period = Rate * seconds, overlap = Rate / 2;
            var raw = new float[period + overlap];
            var random = new Random(731 + room);
            double filtered = 0;
            for (int i = 0; i < raw.Length; i++)
            {
                double t = i / (double)Rate, noise = random.NextDouble() * 2 - 1;
                filtered = filtered * .97 + noise * .03;
                double swell = room == 5 || room == 2 ?
                    .15 + .1 * Math.Sin(t * Math.PI / 2) :
                    .09 + .04 * Math.Sin(t * Math.PI / 4);
                double bird = room < 2 && t > 2 && t < 2.3 ?
                    Math.Sin(t * 2 * Math.PI * (1400 + 150 * Math.Sin(t * 35))) *
                    Math.Sin((t - 2) / .3 * Math.PI) * .03 : 0;
                raw[i] = (float)(filtered * swell + bird);
            }
            var wave = new float[period];
            for (int i = 0; i < period; i++)
            {
                if (i < overlap)
                {
                    // Equal-power gains keep the noise bed audible through
                    // the joint; a simple linear crossfade makes a dip.
                    double phase = i / (double)overlap * (Math.PI / 2);
                    wave[i] = (float)(raw[period + i] * Math.Cos(phase) +
                        raw[i] * Math.Sin(phase));
                }
                else wave[i] = raw[i];
            }
            return wave;
        }
        public static float[] Event(string name)
        {
            if (name == "step") return Footstep("grass", 0);
            if (name == "fishBite") return FishBite();
            if (name == "stoneThrow") return StoneThrow();
            if (name == "stoneImpact") return StoneImpact();
            if (name == "ambush") return AmbushContact();
            if (name == "sentryAlarm") return SentryAlarm();
            if (name == "wayfarerShout") return WayfarerShout();
            if (name == "railDepart") return RailDeparture();
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
        // Original passenger train: two steel wheel contacts beneath an
        // ascending steam whistle. No recorded stock audio, imported music,
        // GUID changes or extra Unity objects. The existing scene's native
        // FrontierAudio registers Events and plays this exact signal only
        // after a paid, verified trip has really reached another station.
        private static float[] RailDeparture()
        {
            const double length=.82;
            var wave=new float[(int)(Rate*length)];
            var random=new Random(173017);
            double whistlePhase=0,steelPhase=0,steamNoise=0;
            double[] railContacts={.06,.32,.63};
            for(int i=0;i<wave.Length;i++)
            {
                double seconds=i/(double)Rate;
                double t=seconds/length;
                double edge=Math.Min(1,seconds*170)*
                    Math.Min(1,(length-seconds)*110);
                double steam=(random.NextDouble()*2-1);
                steamNoise+=(steam-steamNoise)*.11;
                double rising=(seconds-.07)/.48;
                double hornEnvelope=seconds<.07||seconds>.57?0:
                    Math.Min(1,(seconds-.07)*35)*
                    Math.Min(1,(.57-seconds)*25);
                double pitch=510+180*Math.Max(0,Math.Min(1,rising));
                whistlePhase+=2*Math.PI*pitch/Rate;
                double whistle=(Math.Sin(whistlePhase)*.20+
                    Math.Sin(whistlePhase*2)*.065)*hornEnvelope;
                double click=0;
                foreach(double onset in railContacts)
                    if(seconds>=onset)
                        click+=Math.Exp(-(seconds-onset)*95)*
                            (steamNoise*.24+
                             Math.Sin(2*Math.PI*1200*(seconds-onset))*.10);
                steelPhase+=2*Math.PI*88/Rate;
                double wheels=(Math.Sin(steelPhase)*.064+
                    steamNoise*.035)*Math.Sin(Math.PI*t);
                wave[i]=(float)((whistle+click+wheels)*edge);
            }
            return wave;
        }

        // An original, sharp two-beat field call, unmistakably different
        // from ordinary parry/guard, a hostile watch whistle, and a
        // reward. This is the one real-time physical lure action's audio.
        // No imported recording, copyright dependency or Unity GUID.
        private static float[] WayfarerShout()
        {
            var wave=new float[(int)(Rate*.38f)];
            var random=new Random(27911);
            double phase=0, filtered=0;
            for(int i=0;i<wave.Length;i++)
            {
                double t=i/(double)wave.Length;
                double seconds=i/(double)Rate;
                bool second=t>.43;
                double local=second?(t-.43)/.57:t/.43;
                double pitch=second?480-170*local:670-110*local;
                phase+=2*Math.PI*pitch/Rate;
                double breath=random.NextDouble()*2-1;
                filtered+=(breath-filtered)*.13;
                double start=second?.44:0;
                double end=second?1:.41;
                double envelope=Math.Min(1,Math.Max(0,t-start)*95)*
                    Math.Min(1,Math.Max(0,end-t)*65);
                double fundamental=Math.Sin(phase)*.25+
                    Math.Sin(phase*2)*.075+
                    Math.Sin(phase*3)*.035;
                wave[i]=(float)((fundamental+filtered*.05)*
                    envelope);
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
        // Original wrist-release swish, deliberately distinct from the
        // later brittle impact. Only a valid throw emits this player cue;
        // guards investigate the actual landing, not an input event.
        // Deterministic PCM; no imported recording or Unity asset GUID.
        private static float[] StoneThrow()
        {
            var wave=new float[(int)(Rate*.18f)];
            var random=new Random(64109);
            double filtered=0,phase=0;
            for(int i=0;i<wave.Length;i++)
            {
                double t=i/(double)(wave.Length-1);
                filtered+=((random.NextDouble()*2-1)-filtered)*.19;
                phase+=2*Math.PI*(320+680*t)/Rate;
                double envelope=Math.Sin(Math.PI*t)*Math.Min(1,t*40);
                wave[i]=(float)((filtered*.20+Math.Sin(phase)*.095)*envelope);
            }
            return wave;
        }
        // A missed arrow clacks against rock: sharp brittle crack, followed
        // by a short falling stone rattle. This is NOT an enemy-hit jingle.
        // Deterministic samples keep native/CI behavior identical and no
        // external audio files, imports or clip IDs are needed.
        private static float[] StoneImpact()
        {
            var wave=new float[(int)(Rate*.23f)];
            var random=new Random(90423);
            double filtered=0, ringPhase=0;
            for(int i=0;i<wave.Length;i++)
            {
                double t=i/(double)Rate;
                double noise=random.NextDouble()*2-1;
                filtered += (noise-filtered)*.34;
                ringPhase+=2*Math.PI*(950-740*t/.23)/Rate;
                double crack=Math.Exp(-t*58)*filtered*.7;
                double pebble=Math.Exp(-t*17)*Math.Sin(ringPhase)*.16;
                double envelope=Math.Min(1,t*460)*Math.Min(1,(.23-t)*90);
                wave[i]=(float)((crack+pebble)*envelope);
            }
            return wave;
        }
        // A clean, tight rear blade contact sounds nothing like a perfect
        // dodge counter. Real contact alone dispatches this transient.
        // A low metallic strike rolls into a short scabbard/rustle tail.
        private static float[] AmbushContact()
        {
            var wave=new float[(int)(Rate*.29f)];
            var random=new Random(97131);
            double phase=0,filtered=0;
            for(int i=0;i<wave.Length;i++)
            {
                double seconds=i/(double)Rate;
                double t=i/(double)wave.Length;
                double noise=random.NextDouble()*2-1;
                filtered+=(noise-filtered)*.16;
                double f=430-280*t;
                phase+=2*Math.PI*f/Rate;
                double steel=Math.Sin(phase)*Math.Exp(-seconds*19)*.28;
                double edge=filtered*Math.Exp(-seconds*32)*.45;
                double tail=Math.Sin(phase*.48)*Math.Exp(-seconds*12)*.08;
                double envelope=Math.Min(1,seconds*280)*
                    Math.Min(1,(.29-seconds)*65);
                wave[i]=(float)((steel+edge+tail)*envelope);
            }
            return wave;
        }
        // A two-breath sentry whistle, only emitted after a TRUE forward
        // sighting. The short voiced lead and falling second note separate
        // it from the stone-decoy crack, blade strike and dodge counter.
        // Deterministic PCM is registered by native FrontierAudio alongside
        // every existing Event, with no new import GUID or external sample.
        private static float[] SentryAlarm()
        {
            var wave=new float[(int)(Rate*.46f)];
            double phase=0,filtered=0;
            var random=new Random(81753);
            for(int i=0;i<wave.Length;i++)
            {
                double seconds=i/(double)Rate;
                double t=seconds/.46;
                double phrase=t<.48?t/.48:(t-.48)/.52;
                double frequency=t<.48?820-90*phrase:690-210*phrase;
                phase+=2*Math.PI*frequency/Rate;
                filtered+=(random.NextDouble()*2-1-filtered)*.09;
                double attack=t<.48?.04:.515;
                double release=t<.48?.45:.98;
                double envelope=Math.Min(1,Math.Max(0,seconds-attack*.46)*95)*
                    Math.Min(1,Math.Max(0,release*.46-seconds)*72);
                double voice=Math.Sin(phase)*.30+
                    Math.Sin(phase*2)*.07+filtered*.035;
                wave[i]=(float)(voice*envelope);
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

    // Transient ear-level combat intent: keep a truly started battle's music
    // through its recovery/chase beats instead of crossfading to exploration
    // every time a surviving foe leaves windup for a few frames. This lives
    // outside SaveData and is reset on title, practice and room transition.
    public sealed class CombatMusicLinger
    {
        public const float ReleaseSeconds=3f;
        private int room=int.MinValue;
        private float remaining;

        public bool Advance(int currentRoom,bool actualThreat,
            bool livingThreatNearby,bool worldAdvancing,float dt)
        {
            if(currentRoom!=room)
            {room=currentRoom;remaining=0;}
            // This is an in-fight recovery tail, never a ghost encounter.
            // Killing or leaving the last actual foe must allow FrontierAudio
            // to reverse a partially started battle fade immediately.
            if(!livingThreatNearby)
            {remaining=0f;return false;}
            if(actualThreat)
            {remaining=ReleaseSeconds;return true;}
            if(worldAdvancing && !float.IsNaN(dt) && !float.IsInfinity(dt) &&
                dt>0f)
                remaining=Math.Max(0f,remaining-dt);
            return remaining>0f;
        }

        public void Reset()
        {room=int.MinValue;remaining=0f;}
    }
}
