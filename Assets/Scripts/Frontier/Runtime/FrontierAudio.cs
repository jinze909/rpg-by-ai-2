using System;
using System.Collections.Generic;
using UnityEngine;
namespace Starfall
{
    public sealed class FrontierAudio : IDisposable
    {
        private readonly AudioSource[] music = new AudioSource[2];
        private readonly AudioSource effects, ambience, footsteps;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> playedEvents = new HashSet<string>();
        private int active, theme = -1, pending = -1, ambientRoom = -1;
        private float crossfade = 1;
        private int lastContact, contactRoom = -1;
        public FrontierAudio(GameObject host)
        {
            for (int i = 0; i < 2; i++) { music[i] = host.AddComponent<AudioSource>(); music[i].loop = true; }
            effects = host.AddComponent<AudioSource>(); ambience = host.AddComponent<AudioSource>(); ambience.loop = true;
            footsteps = host.AddComponent<AudioSource>(); footsteps.volume = 0;
            foreach (string name in AudioSynthesis.Events) Clip(name, AudioSynthesis.Event(name));
        }
        private AudioClip Clip(string key, float[] samples)
        { AudioClip c; if (clips.TryGetValue(key, out c)) return c; c = AudioClip.Create(key, samples.Length, 1, AudioSynthesis.Rate, false); c.SetData(samples, 0); clips[key] = c; return c; }
        public void Update(GameCore game, bool moving, float dt)
        {
            SaveData save = game.save; bool combat = game.mode == "world" && !game.practicing && game.enemies.Exists(e => e.boss || e.state == 1 && (e.Position - game.Position).Length < 7);
            int desired = AudioSynthesis.Theme(save.room, combat);
            if (desired != theme && desired != pending)
            { pending = desired; crossfade = 0; string key = "theme" + desired; AudioClip c; if (!clips.TryGetValue(key, out c)) c = Clip(key, AudioSynthesis.Music(desired)); music[1 - active].clip = c; music[1 - active].volume = 0; if (save.music) music[1 - active].Play(); }
            if (pending >= 0) { crossfade = Math.Min(1, crossfade + dt / 1.5f); if (crossfade >= 1) { music[active].Stop(); active = 1 - active; theme = pending; pending = -1; } }
            float volume = save.music ? save.musicVolume * .28f : 0;
            music[active].volume = volume * (pending >= 0 ? 1 - crossfade : 1); music[1 - active].volume = volume * (pending >= 0 ? crossfade : 0);
            for (int i = 0; i < 2; i++) { if (!save.music && music[i].isPlaying) music[i].Stop(); else if (save.music && music[i].clip != null && music[i].volume > 0 && !music[i].isPlaying) music[i].Play(); }
            if (ambientRoom != save.room) { ambientRoom = save.room; string key = "ambience" + ambientRoom; AudioClip c; if (!clips.TryGetValue(key, out c)) c = Clip(key, AudioSynthesis.Ambience(ambientRoom)); ambience.Stop(); ambience.clip = c; }
            ambience.volume = save.music && game.mode != "title" ? save.ambienceVolume * .6f : 0;
            if (ambience.volume > 0 && !ambience.isPlaying) ambience.Play(); if (ambience.volume == 0 && ambience.isPlaying) ambience.Stop();
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
            int x = (int)game.save.x, y = (int)game.save.y;
            char tile = y >= 0 && y < game.Map.rows.Length && x >= 0 && x < game.Map.rows[y].Length ? game.Map.rows[y][x] : '.';
            return game.save.room == 9 || game.save.room == 5 && tile == '=' ? "wood" : game.save.room == 7 ? "snow" : game.save.room == 6 || game.save.room == 5 && tile == '%' ? "sand" : game.save.room == 2 || game.save.room == 3 || game.save.room == 4 || game.save.room == 8 || tile == '=' ? "stone" : "grass";
        }
        public void Dispose() { foreach (AudioClip c in clips.Values) UnityEngine.Object.Destroy(c); }
    }
}
