using System;
using System.Collections.Generic;
using System.Reactive;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Deployer.Gui;
using Deployer.Gui.ViewModels;
using ReactiveUI;
using Serilog;

namespace Deployer.Lumia.Gui.ViewModels
{
    public class DeploymentViewModel : ReactiveObject, IBusy
    {
        private readonly IWindowsOptionsProvider optionsProvider;
        private readonly IWoaDeployer deployer;
        private readonly UIServices uiServices;
        private readonly AdvancedViewModel advancedViewModel;
        private readonly WimPickViewModel wimPickViewModel;
        private readonly IFileSystemOperations fileSystemOperations;
        private readonly ISettingsService settingsService;
        private readonly ObservableAsPropertyHelper<bool> isBusyHelper;

        public DeploymentViewModel(
            IWindowsOptionsProvider optionsProvider,
            IWoaDeployer deployer, UIServices uiServices, AdvancedViewModel advancedViewModel,
            WimPickViewModel wimPickViewModel, IFileSystemOperations fileSystemOperations, ISettingsService settingsService)
        {
            this.optionsProvider = optionsProvider;
            this.deployer = deployer;
            this.uiServices = uiServices;
            this.advancedViewModel = advancedViewModel;
            this.wimPickViewModel = wimPickViewModel;
            this.fileSystemOperations = fileSystemOperations;
            this.settingsService = settingsService;

            var isSelectedWim = wimPickViewModel.WhenAnyObservable(x => x.WimMetadata.SelectedImageObs)
                .Select(metadata => metadata != null);

            FullInstallWrapper = new CommandWrapper<Unit, Unit>(this,
                ReactiveCommand.CreateFromTask(Deploy, isSelectedWim), uiServices.Dialog);
            IsBusyObservable = FullInstallWrapper.Command.IsExecuting;
            isBusyHelper = IsBusyObservable.ToProperty(this, model => model.IsBusy);
        }

        public bool IsBusy => isBusyHelper.Value;

        private async Task Deploy()
        {
            Log.Information("# Starting deployment...");

            var windowsDeploymentOptions = new WindowsDeploymentOptions
            {
                ImagePath = wimPickViewModel.WimMetadata.Path,
                ImageIndex = wimPickViewModel.WimMetadata.SelectedDiskImage.Index,
                SizeReservedForWindows = advancedViewModel.SizeReservedForWindows,
                UseCompact = advancedViewModel.UseCompactDeployment,
            };

            optionsProvider.Options = windowsDeploymentOptions;

            await CleanDownloadedIfNeeded();
            await deployer.Deploy();

            Log.Information("Deployment successful");

            await uiServices.Dialog.PickOptions(Resources.WindowsDeployedSuccessfully, new List<Option>()
            {
                new Option("Close")
            });
        }

        private async Task CleanDownloadedIfNeeded()
        {
            if (!settingsService.CleanDownloadedBeforeDeployment)
            {
                return;
            }

            if (fileSystemOperations.DirectoryExists(AppPaths.DownloadedFolderName))
            {
                Log.Information("Deleting previously downloaded deployment files");
                await fileSystemOperations.DeleteDirectory(AppPaths.DownloadedFolderName);
            }
        }

        public CommandWrapper<Unit, Unit> FullInstallWrapper { get; set; }
        public IObservable<bool> IsBusyObservable { get; }
    }
}