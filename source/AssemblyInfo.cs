using CesiumLoader.SDK.Manifests;

[assembly: ModManifest("随机选人", "1.2.0", "hpenguins",
    "选人阶段点屏幕右侧的按钮，模拟随机点击多张满级角色卡并随机落定一个；不会自动确认，被抢时自动重抽，按钮位置可调。",
    Permissions = ModPermission.ReadGameState | ModPermission.GameActions | ModPermission.FileWrite | ModPermission.Input,
    SdkVersion = "3.8.5",
    Platforms = ModPlatform.Windows | ModPlatform.Android)]
