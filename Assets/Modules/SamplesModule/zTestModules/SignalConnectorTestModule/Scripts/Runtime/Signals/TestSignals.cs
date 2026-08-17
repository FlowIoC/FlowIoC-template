using FlowIoC.BaseModule.Signals;

namespace Modules.SamplesModule.SignalConnectorTestModule.Signals
{
    public class ASignals : ISignalHolder
    {
        public Signal NoParameterSignal = new Signal();
        public Signal<int> OneParameterSignal = new Signal<int>();
        public Signal<string, int> TwoParameterSignal = new Signal<string, int>();
        public Signal<string, int, int> ThreeParameterSignal = new Signal<string, int, int>();
        public Signal<string, int, int, int> FourParameterSignal = new Signal<string, int, int, int>();
    }
    public class BSignals : ISignalHolder
    {
        public Signal NoParameterSignal = new Signal();
        public Signal<int> OneParameterSignal = new Signal<int>();
        public Signal<string, int> TwoParameterSignal = new Signal<string, int>();
        public Signal<string, int, int> ThreeParameterSignal = new Signal<string, int, int>();
        public Signal<string, int, int, int> FourParameterSignal = new Signal<string, int, int, int>();
    }
    
}