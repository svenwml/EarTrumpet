﻿using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Devices.Enumeration;
using Windows.Devices.Midi;
using System.Threading.Tasks;

namespace EarTrumpet.DataModel.MIDI
{
    public static class MidiIn
    {
        // This type is just awful
        private static Dictionary<string, Dictionary<Tuple<byte, byte>, List<Action<MidiControlChangeMessage>>>>
            callbacks;

        private static List<Action<MidiInPort, MidiControlChangeMessage>> generalCallbacks;
        private static DeviceWatcher deviceWatcher;
        private static List<string> watchedDevices;

        internal static void AddGeneralCallback(Action<MidiInPort, MidiControlChangeMessage> callback)
        {
            generalCallbacks.Add(callback);
        }
        
        internal static void AddControlChangeCallback(string id, Action<MidiControlChangeMessage> callback,
            byte channel = 255, byte controller = 255)
        {
            if (!callbacks.ContainsKey(id))
            {
                callbacks.Add(id, new Dictionary<Tuple<byte, byte>, List<Action<MidiControlChangeMessage>>>());
                _StartListening(id);
            }
            
            var key = new Tuple<byte, byte>(channel, controller);
            if (!callbacks[id].ContainsKey(key))
            {
                callbacks[id].Add(key, new List<Action<MidiControlChangeMessage>>());
            }
            
            callbacks[id][key].Add(callback);
        }

        private static void MessageReceived(MidiInPort sender, MidiMessageReceivedEventArgs args)
        {
            IMidiMessage received = args.Message;

            if (received.Type == MidiMessageType.ControlChange)
            {
                MidiControlChangeMessage msg = (MidiControlChangeMessage) received;
                foreach (var callback in generalCallbacks)
                {
                    callback(sender, msg);
                }
                
                string id = sender.DeviceId;
                if (!callbacks.ContainsKey(id))
                {
                    return;
                }
                
                
                
                var key = new Tuple<byte, byte>(msg.Channel, msg.Controller);
                if (callbacks[id].ContainsKey(key))
                {
                    foreach (var callback in callbacks[id][key])
                    {
                        callback(msg);
                    }
                }

                var universalKey = new Tuple<byte, byte>(255, 255);
                if (callbacks[id].ContainsKey(universalKey))
                {
                    foreach (var callback in callbacks[id][universalKey])
                    {
                        callback(msg);
                    }
                }
            }
        }

        internal static void _StartListening(string id)
        {
            async Task StartListening()
            {
                var inPort = await MidiInPort.FromIdAsync(id);
            
                inPort.MessageReceived += MessageReceived;
            }

            if (watchedDevices.Contains(id))
            {
                return;
            }
            
            Task.Run(async () => await StartListening()).Wait();
            watchedDevices.Add(id);
        }
        
        private static void Added(DeviceWatcher sender, DeviceInformation args)
        {
            if (callbacks.ContainsKey(args.Id))
            {
                _StartListening(args.Id);
            }
        }
        
        private static void Removed(DeviceWatcher sender, DeviceInformationUpdate args)
        {
            if (watchedDevices.Contains(args.Id))
            {
                watchedDevices.Remove(args.Id);
            }
        }

        static MidiIn()
        {
            callbacks = new Dictionary<string, Dictionary<Tuple<byte, byte>, List<Action<MidiControlChangeMessage>>>>();
            generalCallbacks = new List<Action<MidiInPort, MidiControlChangeMessage>>();
            watchedDevices = new List<string>();
            
            deviceWatcher = DeviceInformation.CreateWatcher(MidiInPort.GetDeviceSelector());
            deviceWatcher.Added += Added;
            deviceWatcher.Removed += Removed;
            
            deviceWatcher.Start();
        }
        
        private static async Task<List<MidiInDevice>> _GetAllDevices(bool returnEmptyNames=false)
        {
            var midiInputQueryString = MidiInPort.GetDeviceSelector();
            var midiInputDevices = await DeviceInformation.FindAllAsync(midiInputQueryString);

            return (from device in midiInputDevices where returnEmptyNames || device.Name.Length != 0 select new MidiInDevice(device)).ToList();
        }

        public static List<MidiInDevice> GetAllDevices(bool returnEmptyNames=false)
        {
            var t = Task.Run(async () => await _GetAllDevices(returnEmptyNames));
            return t.Result;
        }

        public static MidiInDevice GetDeviceByName(string name)
        {
            return GetAllDevices().FirstOrDefault(device => device.Name == name);
        }
        
        public static MidiInDevice GetDeviceById(string id)
        {
            return GetAllDevices().FirstOrDefault(device => device.Id == id);
        }
    }
}