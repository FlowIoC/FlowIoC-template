using FlowIoC.BaseModule.Connectors;
using FlowIoC.BaseModule.Constructables;
using FlowIoC.BaseModule.Injectable.Attributes;
using Modules.SamplesModule.SignalConnectorTestModule.Signals;
using UnityEngine;

namespace Modules.SamplesModule.SignalConnectorTestModule.Models
{
    public class SignalConnectorModel : IConstructable
    {
        [InjectSignal] private ASignals _aSignals { get; set; }
        [InjectSignal] private BSignals _bSignals { get; set; }
        
        public void PostConstruct()
        {
            _aSignals.NoParameterSignal.Connect(_bSignals.NoParameterSignal,"Aconnections");
            _aSignals.OneParameterSignal.Connect(_bSignals.OneParameterSignal);
            _aSignals.OneParameterSignal.Connect(_bSignals.OneParameterSignal);
            _bSignals.OneParameterSignal.Connect(score => { Debug.Log($"OneParameterSignal: {score}"); });
            
            _aSignals.TwoParameterSignal.Connect(_bSignals.TwoParameterSignal);
            _aSignals.ThreeParameterSignal.Connect(_bSignals.ThreeParameterSignal);
        }
        void OnDeconstruct()
        {
            SignalConnector.DisconnectAll();
            
            // _aSignals.OneParameterSignal.Disconnect("Aconnections");
            // SignalConnector.DisconnectGroup("Aconnections");
        }

        public bool IsPostConstructed { get; set; }
        public bool IsDeConstructed { get; set; }
    }
}