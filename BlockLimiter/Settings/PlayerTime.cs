using System;
using System.Collections.Concurrent;
using System.Collections.Specialized;
using Sandbox.ModAPI;
using Torch;

namespace BlockLimiter.Settings
{
    [Serializable]
    public class PlayerTime:ViewModel
    {
        private DateTime _time;
        private ulong _player;
        public PlayerTime()
        {
            CollectionChanged += OnCollectionChanged;
        }
        private void OnCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged();
            BlockLimiterConfig.Instance.Save();
        }

        public DateTime Time
        {
            get => _time;
            set
            {
                _time = value;
                OnPropertyChanged();
            }
        }

        public ulong Player
        {
            get=> _player;
            set
            {
                _player = value;
                OnPropertyChanged();
            }
        } 
    }
}