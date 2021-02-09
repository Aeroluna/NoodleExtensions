namespace NoodleExtensions
{
    using Zenject;

    public class NoodleInstaller : Installer
    {
        internal static DiContainer DiContainer { get; private set; }

        public override void InstallBindings()
        {
            DiContainer = Container;
        }
    }
}
