# MSC Multiplayer 0.2.0-dev

My Summer Car: UDP-сервер (.NET 8) + мод под MSCLoader (Unity 5.0.0f4, x86 Mono, net35). Порт 27020.

## Реализовано
Лобби F8 (IP, порт, ник, статус, свой ID, список, счётчик N/4); создание и удаление удалённых
персонажей; ник над головой без смещения на любой дистанции; синхронизация позиции, поворота
корпуса, наклона головы, скорости и состояния; интерполяция с буфером и защита от устаревших
пакетов; анимации Idle/Walking/Running/Jumping/Falling; артикулированная модель и интерфейс
IAvatarModel для замены на игровую; режим игровой модели + Scan scene models; сервер читает
server.json, лимитирует частоту, валидирует координаты, держит 4 слота.

## Сборка
Нужен .NET SDK 8+ и игра с MSCLoader.
1. setx MSC_GAME_DIR "C:\Games\My Summer Car"   (или запусти START_HERE.bat, он спросит)
2. dotnet build src\MSC.Server\MSC.Server.csproj -c Release
3. dotnet build src\MSC.Client\MSC.Client.csproj -c Release
SDK сам подтянет Microsoft.NETFramework.ReferenceAssemblies.net35 (первый restore — с интернетом).
MSCLoader.dll, UnityEngine.dll, Assembly-CSharp.dll берутся из %MSC_GAME_DIR%\mysummercar_Data\Managed\.

Результат:
  src\MSC.Client\bin\Release\net35\MSC.Multiplayer.Client.dll
  src\MSC.Server\bin\Release\net8.0\MSC.Server.exe

## Установка
1. MSC.Multiplayer.Client.dll -> папка Mods MSCLoader.
2. Запусти MSC.Server.exe (server.json положи рядом, если меняешь настройки).
3. Разреши входящий UDP 27020 в брандмауэре.
4. В игре F8 -> IP, порт, ник -> Connect.

## Два ПК через Radmin VPN
1. Оба ПК в одной сети Radmin, адрес серверного ПК вида 26.x.x.x.
2. На серверном ПК: сервер запущен, UDP 27020 разрешён (частный и публичный профиль).
3. На обоих ПК в лобби: IP серверного ПК, порт 27020, разные ники.
4. Ждём Status: Connected (N remote), Players online: 2/4, в консоли сервера [JOIN].
5. Движение: аватары идут и бегут без рывков, ник держится над головой на любой дистанции.
6. Disconnect: аватар и ник исчезают, в консоли [LEAVE].

## Архитектура
NetworkClient - транспорт; MotionSender - отправка POS2; RemoteMotion - буфер, интерполяция,
парсер ST2/STATE; PlayerSyncManager - список игроков; AvatarModels - IAvatarModel и процедурная
модель; GameAvatarModel - клон игровой модели и фабрика; RemoteAvatarManager - жизненный цикл;
Nametag - точка над головой и отрисовка; MSCMPMod - вход MSCLoader и лобби.

## Ограничения
Модель временная; игровая берётся только при наличии skinned-меша с пригодными legacy-клипами.
Состояние движения - эвристика по скорости. В машине PLAYER деактивируется, позиция замирает;
синхронизации Satsuma нет. UDP без авторизации. Высота модели правится через
RemoteAvatarManager.VerticalOffset (PlayerPrefs MSCMP_VOffset).

## Упаковка
powershell -ExecutionPolicy Bypass -File tools\make_release.ps1