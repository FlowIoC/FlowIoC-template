#if UNITY_EDITOR
using FlowIoC.BaseModule.Connectors;
using FlowIoC.BaseModule.Contexts;
using Modules.SamplesModule.SignalConnectorTestModule.Models;
using Modules.SamplesModule.SignalConnectorTestModule.Signals;
using UnityEngine;

namespace Modules.SamplesModule.SignalConnectorTestModule.RootsContexts
{
   
    public class SignalConnectorTestContext : Context
    {
        private ASignals _aSignals;
        private BSignals _bSignals;
        public override void SignalBindings()
        {
            base.SignalBindings();
            _aSignals = InjectionBinder.Bind<ASignals>();
            _bSignals = InjectionBinder.Bind<BSignals>();
        }

        public override void InjectionBindings()
        {
            base.InjectionBindings();
            InjectionBinder.Bind<SignalConnectorModel>();

        }

        public override void MediationBindings()
        {
            base.MediationBindings();
        }

        public override void CommandBindings()
        {
            base.CommandBindings();
        }

        public override void Setup()
        {
            base.Setup();
        }

        public override void Launch()
        {
            base.Launch();
            
            
            // _aSignals.NoParameterSignal.Dispatch();
            _aSignals.OneParameterSignal.Dispatch(50);
            _aSignals.OneParameterSignal.Dispatch(50);
            _bSignals.OneParameterSignal.Dispatch(100);
            // _aSignals.OneParameterSignal.Disconnect();
            // _aSignals.OneParameterSignal.Dispatch(100);
            // _aSignals.TwoParameterSignal.Dispatch("Hello",100);
            
        }
    }
}
#endif
