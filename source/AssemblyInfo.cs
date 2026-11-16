using CesiumLoader.SDK.Manifests;

[assembly: ModManifest("随机选人", "1.0.0", "hpenguins",
    "选人阶段模拟随机点击多张满级角色卡，最后随机落定一个并自动确认；被抢时自动重抽。",
    Permissions = ModPermission.ReadGameState | ModPermission.GameActions | ModPermission.FileWrite | ModPermission.Input,
    SdkVersion = "3.8.5",
    Platforms = ModPlatform.Windows | ModPlatform.Android)]
