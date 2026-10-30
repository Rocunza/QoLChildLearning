namespace ZCN.QoL.ChildLearning
{
    internal static class ChildLearningDebug
    {
        internal static bool Enabled
        {
            get { return ChildLearningMod.Settings != null && ChildLearningMod.Settings.enableDebugLogging; }
        }
    }
}
