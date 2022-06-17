using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Media;

namespace Mariasek.SharedClient
{
    public class AssetLoader
    {
        private ContentManager _content;
        private Dictionary<string, Texture2D> _textures;
        private Dictionary<string, SoundEffect> _soundEffects;
        private Dictionary<string, Song> _songs;
        private List<string> _errors;

        public int TotalCount => _textures.Count() + _soundEffects.Count() + _songs.Count();
        public string ErrorMessage { get; private set; }

        public bool ContentLoaded
        {
            get
            {
                return _textures.All(i => _errors.Contains(i.Key) ||
                                          (i.Value != null &&
                                           !i.Value.IsDisposed)) &&
                       _soundEffects.All(i => _errors.Contains(i.Key) ||
                                              (i.Value != null &&
                                               !i.Value.IsDisposed)) &&
                       _songs.All(i => _errors.Contains(i.Key) ||
                                       (i.Value != null &&
                                        !i.Value.IsDisposed));
            }
        }

        public AssetLoader(ContentManager content, string[] textures, string[] soundEffects, string[] songs)
        {
            _content = content;
            _textures = new Dictionary<string, Texture2D>(textures.Select(i => new KeyValuePair<string, Texture2D>(i, null)));
            _soundEffects = new Dictionary<string, SoundEffect>(soundEffects.Select(i => new KeyValuePair<string, SoundEffect>(i, null)));
            _songs = new Dictionary<string, Song>(songs.Select(i => new KeyValuePair<string, Song>(i, null)));
            _errors = new List<string>();
        }

        //returns true if an asset was loaded, false if there are no more assets to be loaded remaining
        public bool LoadOneAsset()
        {
            MariasekMonoGame.Log("LoadOneAsset()");

            var kvp1 = _textures.FirstOrDefault(i => i.Value == null || i.Value.IsDisposed);
            var found = kvp1.Key != null;

            if (found)
            {
                MariasekMonoGame.Log($"Loading { kvp1.Key}");
                _textures[kvp1.Key] = _content.Load<Texture2D>(kvp1.Key);
            }
            else
            {
                var kvp2 = _soundEffects.FirstOrDefault(i => !_errors.Contains(i.Key) &&
                                                             (i.Value == null || i.Value.IsDisposed));
                found = kvp2.Key != null;

                if (found)
                {
                    MariasekMonoGame.Log($"Loading { kvp2.Key}");
                    try
                    {
                        _soundEffects[kvp2.Key] = _content.Load<SoundEffect>(kvp2.Key);
                    }
                    catch(Exception ex)
                    {
                        _errors.Add(kvp2.Key);
                        if (string.IsNullOrEmpty(ErrorMessage))
                        {
                            ErrorMessage = $"Chyba při inicializaci zvukového efektu \"{kvp2.Key}\":\n{ex.Message}";
                        }
                        return false;
                    }
                }
                else
                {
                    var kvp3 = _songs.FirstOrDefault(i => !_errors.Contains(i.Key) &&
                                                          (i.Value == null || i.Value.IsDisposed));
                    found = kvp3.Key != null;

                    if (found)
                    {
                        MariasekMonoGame.Log($"Loading { kvp3.Key}");
                        try
                        {
                            _songs[kvp3.Key] = _content.Load<Song>(kvp3.Key);
                        }
                        catch(Exception ex)
                        {
                            _errors.Add(kvp3.Key);
                            if (string.IsNullOrEmpty(ErrorMessage))
                            {
                                ErrorMessage = $"Chyba při inicializaci zvukové stopy \"{kvp3.Key}\":\n{ex.Message}";
                            }
                            return false;
                        }
                    }
                }
            }
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                MariasekMonoGame.Log(ErrorMessage);
            }

            return !ContentLoaded;
        }

        public Texture2D GetTexture(string key)
        {
            if (!_textures.ContainsKey(key))
            {
                return null;
            }
            return _textures[key];
        }

        public SoundEffect GetSoundEffect(string key)
        {
            if (!_soundEffects.ContainsKey(key))
            {
                return null;
            }
            return _soundEffects[key];
        }

        public Song GetSong(string key)
        {
            if (!_songs.ContainsKey(key))
            {
                return null;
            }
            return _songs[key];
        }
    }
}
