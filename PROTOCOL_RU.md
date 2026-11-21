# Протокол MSC1 (UDP, UTF-8)

Клиент -> сервер:
- `MSC1|HELLO|nickname`
- `MSC1|PING`
- `MSC1|POS|x|y|z|rotationY`
- `MSC1|BYE`

Сервер -> клиент:
- `MSC1|WELCOME|id|maxPlayers`
- `MSC1|ERROR|reason`
- `MSC1|PONG`
- `MSC1|PLAYER_JOIN|id|nickname`
- `MSC1|PLAYER_LEAVE|id`
- `MSC1|ROSTER|id|nickname|x|y|z|rotationY`
- `MSC1|STATE|id|nickname|x|y|z|rotationY`

Числа используют invariant culture. ID клиента сервер определяет по UDP endpoint,
а не принимает от клиента. UDP не гарантирует доставку/порядок пакетов.
