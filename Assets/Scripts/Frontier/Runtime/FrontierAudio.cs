using System;
using System.Collections.Generic;
using UnityEngine;
namespace Starfall
{
    public sealed class FrontierAudio : IDisposable
    {
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly AudioSource effects, footsteps;
        // Two looping atmosphere channels make a real room transition audible
        // without clipping the old landscape at the region boundary.
        private AudioSource ambience, ambienceIncoming;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> playedEvents = new HashSet<string>();
        private int active, theme = -1, pending = -1, ambientRoom = -1, ambientPending = -1;
        private float crossfade = 1, ambientFade;
        private int lastContact, contactRoom = -1;
        private readonly CombatMusicLinger combatLinger=new CombatMusicLinger();
        public FrontierAudio(GameObject host)
        {
            for (int i = 0; i < 2; i++) { music[i] = host.AddComponent<AudioSource>(); music[i].loop = true; }
            effects = host.AddComponent<AudioSource>();
            ambience = host.AddComponent<AudioSource>(); ambience.loop = true;
            ambienceIncoming = host.AddComponent<AudioSource>(); ambienceIncoming.loop = true;
            footsteps = host.AddComponent<AudioSource>(); footsteps.volume = 0;
            foreach (string name in AudioSynthesis.Events) Clip(name, AudioSynthesis.Event(name));
        }
        private AudioClip Clip(string key, float[] samples)
        { AudioClip c; if (clips.TryGetValue(key, out c)) return c; c = AudioClip.Create(key, samples.Length, 1, AudioSynthesis.Rate, false); c.SetData(samples, 0); clips[key] = c; return c; }
        public void Update(GameCore game, bool moving, float dt)
        {
            SaveData save = game.save;
            // Opening a backpack, map or pause does not make a living Boss
            // disappear. Those menus suspend gameplay while the same physical
            // encounter remains; keep its song and existing fade phase.
            // The title has no live fight even if an old world object lingers.
            bool encounterExists = game.mode != "title" && !game.practicing &&
                game.enemies.Exists(e => e != null && e.hp > 0f &&
                    (e.boss || e.state == 1 &&
                        (e.Position - game.Position).Length < 7f));
            // An ordinary foe is intermittently in its windup/recovery
            // state. Without a small transient release tail, the real
            // 1.5-second AudioSource crossfade repeatedly reverses during
            // one continuous fight. Paused menus suspend that tail; title
            // and practice never inherit the previous room's combat music.
            if(game.mode=="title" || game.practicing)combatLinger.Reset();
            bool combat = game.mode!="title" && !game.practicing &&
                combatLinger.Advance(save.room,encounterExists,
                    game.mode=="world",dt);
            int desired = AudioSynthesis.Theme(save.room, combat);
            // Combat proximity often changes again before a 1.5-second fade
            // completes. When the original theme becomes desirable again,
            // reverse the same pair of live channels instead of finishing
            // the WRONG battle track and crossfading back a second time.
            if (pending >= 0 && desired == theme)
            {
                crossfade = Math.Max(0, crossfade - Math.Max(0, dt) / 1.5f);
                if (crossfade <= 0)
                {
                    music[1 - active].Stop();
                    pending = -1;
                }
            }
            else
            {
                if (desired != theme && desired != pending)
                {
                    pending = desired;
                    crossfade = 0;
                    string key = "theme" + desired;
                    AudioClip c;
                    if (!clips.TryGetValue(key, out c))
                        c = Clip(key, AudioSynthesis.Music(desired));
                    // A third region/theme can replace a partial incoming
                    // track: stop the old source before swapping its clip.
                    music[1 - active].Stop();
                    music[1 - active].clip = c;
                    music[1 - active].volume = 0;
                    if (save.music) music[1 - active].Play();
                }
                if (pending >= 0)
                {
                    crossfade = Math.Min(1, crossfade + Math.Max(0, dt) / 1.5f);
                    if (crossfade >= 1)
                    {
                        music[active].Stop();
                        active = 1 - active;
                        theme = pending;
                        pending = -1;
                    }
                }
            }
            float volume = save.music ? save.musicVolume * .28f : 0;
            music[active].volume = volume * (pending >= 0 ? 1 - crossfade : 1); music[1 - active].volume = volume * (pending >= 0 ? crossfade : 0);
            for (int i = 0; i < 2; i++) { if (!save.music && music[i].isPlaying) music[i].Stop(); else if (save.music && music[i].clip != null && music[i].volume > 0 && !music[i].isPlaying) music[i].Play(); }
            // The world can switch rooms again while an atmosphere crossfade
            // is still playing. Reverse when returning to the outgoing room;
            // for a third room keep the louder of the two actual landscapes.
            // Neither save state nor GameCore simulation is changed.
            if (ambientPending >= 0 && save.room == ambientRoom)
            {
                ambientFade = Math.Max(0, ambientFade - Math.Max(0, dt) / 1.2f);
                if (ambientFade <= 0)
                {
                    ambienceIncoming.Stop();
                    ambientPending = -1;
                }
            }
            else
            {
                if (save.room != ambientRoom && save.room != ambientPending)
                {
                    if (ambientPending >= 0 && ambientFade >= .5f)
                    {
                        ambience.Stop();
                        AudioSource former = ambience;
                        ambience = ambienceIncoming;
                        ambienceIncoming = former;
                        ambientRoom = ambientPending;
                    }
                    else if (ambientPending >= 0)
                        ambienceIncoming.Stop();
                    ambientPending = save.room;
                    ambientFade = 0;
                    string key = "ambience" + ambientPending;
                    AudioClip c;
                    if (!clips.TryGetValue(key, out c))
                        c = Clip(key, AudioSynthesis.Ambience(ambientPending));
                    ambienceIncoming.Stop();
                    ambienceIncoming.clip = c;
                    ambienceIncoming.volume = 0;
                }
                if (ambientPending >= 0)
                {
                    ambientFade = Math.Min(1, ambientFade + Math.Max(0, dt) / 1.2f);
                    if (ambientFade >= 1)
                    {
                        ambience.Stop();
                        AudioSource former = ambience;
                        ambience = ambienceIncoming;
                        ambienceIncoming = former;
                        ambientRoom = ambientPending;
                        ambientPending = -1;
                    }
                }
            }
            float ambientVolume = save.music && game.mode != "title" ?
                save.ambienceVolume * .6f : 0;
            ambience.volume = ambientVolume * (ambientPending >= 0 ? 1 - ambientFade : 1);
            ambienceIncoming.volume = ambientVolume * (ambientPending >= 0 ? ambientFade : 0);
            if (ambience.volume > 0 && !ambience.isPlaying && ambience.clip != null)
                ambience.Play();
            if (ambience.volume == 0 && ambience.isPlaying)
                ambience.Stop();
            if (ambienceIncoming.volume > 0 && !ambienceIncoming.isPlaying && ambienceIncoming.clip != null)
                ambienceIncoming.Play();
            if (ambienceIncoming.volume == 0 && ambienceIncoming.isPlaying)
                ambienceIncoming.Stop();
            effects.volume = save.music ? save.effectsVolume * .3f : 0;
            footsteps.volume = save.music && game.mode == "world" ? save.effectsVolume * .07f : 0;
            if (contactRoom != save.room || game.footContacts < lastContact) { contactRoom = save.room; lastContact = game.footContacts; }
            if (game.footContacts > lastContact && footsteps.volume > 0 && game.ActuallyMoving && game.dashTime <= 0 && !game.Dying)
            {
                int variation = game.footContacts % 7; string surface = FootSurface(game), key = "foot-" + surface + "-" + variation;
                AudioClip clip; if (!clips.TryGetValue(key, out clip)) clip = Clip(key, AudioSynthesis.Footstep(surface, variation));
                footsteps.pitch = .94f + variation * .02f; footsteps.PlayOneShot(clip, .8f + variation * .025f);
            }
            lastContact = game.footContacts;
            playedEvents.Clear(); int count = 0; while (game.sounds.Count > 0 && count++ < 24) { string name = game.sounds.Dequeue(); AudioClip c; if (playedEvents.Add(name) && effects.volume > 0 && clips.TryGetValue(name, out c)) effects.PlayOneShot(c); }
        }
        internal static string FootSurface(GameCore game)
        {
            return FootSurface(game,game.Position);
        }
        // One terrain mapping for audio and visual contacts. An enemy or
        // companion must sample the ground under ITS feet, not the hero's.
        internal static string FootSurface(GameCore game,Point foot)
        {
            int x=(int)Math.Floor(foot.x),y=(int)Math.Floor(foot.y);
            char tile=y>=0&&y<game.Map.rows.Length&&x>=0&&
                x<game.Map.rows[y].Length?game.Map.rows[y][x]:'.';
            return game.save.room==9 ||
                game.save.room==5&&tile=='='?"wood":
                game.save.room==7?"snow":
                game.save.room==6||game.save.room==5&&tile=='%'?"sand":
                game.save.room==2||game.save.room==3||
                game.save.room==4||game.save.room==8||tile=='='?"stone":"grass";
        }
        public void Dispose() { foreach (AudioClip c in clips.Values) UnityEngine.Object.Destroy(c); }
    }
}
