# MSC Multiplayer 0.1.10-dev — исправление сборки клиента

В предыдущем архиве пункт сборки клиента не запрашивал `MSC_GAME_DIR`. Поэтому MSBuild искал ссылки на DLL по пустому пути и выдавал MSB3245 / CS0246.

Теперь `START_HERE.bat`, пункт 3:
1. Запрашивает корневую папку My Summer Car.
2. Проверяет `mysummercar_Data\Managed\MSCLoader.dll`.
3. Проверяет `mysummercar_Data\Managed\UnityEngine.dll`.
4. Проверяет `mysummercar_Data\Managed\Assembly-CSharp.dll`.
5. Запускает сборку только если все три файла найдены.

DLL игры в архив не включены. Саму C# сборку в этой среде выполнить нельзя; её результат нужно проверить на Windows.
