# Протокол MSC1 (0.2.0)

UDP, порт 27020, UTF-8, разделитель |, пакет <= 512 байт, числа с точкой.

## Служебные (без изменений с 0.1.x)
C->S MSC1|HELLO|nick                                  (3)
S->C MSC1|WELCOME|id|maxPlayers                       (4)
S->C MSC1|PLAYER_JOIN|id|nick                         (4)
S->C MSC1|PLAYER_LEAVE|id                             (3)
S->C MSC1|ROSTER|id|nick|x|y|z|rot                    (8)
C->S MSC1|PING  /  S->C MSC1|PONG                     (2)
C->S MSC1|BYE                                         (2)
S->C MSC1|ERROR|server_full / server_shutdown         (3)

## Движение v1 (совместимость)
C->S MSC1|POS|x|y|z|rot                               (6)
S->C MSC1|STATE|id|nick|x|y|z|rot                     (8)

## Движение v2
C->S MSC1|POS2|seq|x|y|z|yaw|pitch|state|vx|vy|vz|tms          (13)
S->C MSC1|ST2|id|seq|x|y|z|yaw|pitch|state|vx|vy|vz|tms        (14)

seq   - ushort, циклический счётчик; новее считается (ushort)(a-b) < 32768 && a != b
yaw   - 0..360, pitch - -90..90
state - 0 Idle, 1 Walking, 2 Running, 3 Jumping, 4 Falling
vx/vy/vz - м/с, >60 м/с сервер обнуляет
tms   - время отправителя, только диагностика; интерполяция идёт по локальному времени приёма

## Правила
- Лимит maxPacketsPerSecond (50) на игрока, лишнее отбрасывается.
- |x|,|y|,|z| <= 10000, NaN/Infinity отбрасываются.
- Таймаут игрока timeoutSeconds (30).
- Смешанная сессия: v1-клиентам сервер дополнительно шлёт STATE. Клиент 0.2.0 понимает
  оба формата и после первого ST2 игнорирует STATE для этого игрока.
- Клиент: 15 Гц и только при изменении (>1 см, >0.5 град, смена состояния) + heartbeat 0.5 с.
- Интерполяция 120 мс, экстраполяция <=250 мс, скачок >6 м = телепорт (мгновенно, скорость обнуляется).