using UnityEngine;

namespace BuildATower
{
    public enum AudioBus
    {
        Sfx,
        Music,
        Ambience
    }

    public class AudioBuses
    {
        const string KeyMute = "bat_audio_mute";
        const string KeyMaster = "bat_audio_master";
        const string KeySfx = "bat_audio_sfx";
        const string KeyMusic = "bat_audio_music";
        const string KeyAmbience = "bat_audio_ambience";

        const float DefaultMaster = 0.70f;
        const float DefaultSfx = 0.70f;
        const float DefaultAmbience = 0.70f;
        const float DefaultMusic = 0.50f;

        bool _masterMute;
        float _master = DefaultMaster;
        float _sfx = DefaultSfx;
        float _music = DefaultMusic;
        float _ambience = DefaultAmbience;

        public bool MasterMute
        {
            get => _masterMute;
            set => _masterMute = value;
        }

        public float Master
        {
            get => _master;
            set => _master = Clamp01(value);
        }

        public float Sfx
        {
            get => _sfx;
            set => _sfx = Clamp01(value);
        }

        public float Music
        {
            get => _music;
            set => _music = Clamp01(value);
        }

        public float Ambience
        {
            get => _ambience;
            set => _ambience = Clamp01(value);
        }

        public float Effective(AudioBus bus)
        {
            if (MasterMute) return 0f;
            return Master * BusVolume(bus);
        }

        public void Load()
        {
            MasterMute = PlayerPrefs.GetInt(KeyMute, 0) != 0;
            Master = PlayerPrefs.GetFloat(KeyMaster, DefaultMaster);
            Sfx = PlayerPrefs.GetFloat(KeySfx, DefaultSfx);
            Music = PlayerPrefs.GetFloat(KeyMusic, DefaultMusic);
            Ambience = PlayerPrefs.GetFloat(KeyAmbience, DefaultAmbience);
        }

        public void Save()
        {
            PlayerPrefs.SetInt(KeyMute, MasterMute ? 1 : 0);
            PlayerPrefs.SetFloat(KeyMaster, Master);
            PlayerPrefs.SetFloat(KeySfx, Sfx);
            PlayerPrefs.SetFloat(KeyMusic, Music);
            PlayerPrefs.SetFloat(KeyAmbience, Ambience);
            PlayerPrefs.Save();
        }

        float BusVolume(AudioBus bus) => bus switch
        {
            AudioBus.Sfx => Sfx,
            AudioBus.Music => Music,
            AudioBus.Ambience => Ambience,
            _ => 1f
        };

        static float Clamp01(float value) => value < 0f ? 0f : value > 1f ? 1f : value;
    }
}
