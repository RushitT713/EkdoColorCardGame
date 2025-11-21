using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ColorCardGame.Models;
using ColorCardGame.Services;

namespace ColorCardGame.Hubs
{
    public class ColorCardHub : Hub
    {
        private static readonly ConcurrentDictionary<string, ColorCardLobby> Lobbies = new();
        private readonly IStatsService _statsService;

        public ColorCardHub(IStatsService statsService)
        {
            _statsService = statsService;
        }

        public async Task JoinLobby(string lobbyCode, string playerName)
        {
            var httpContext = Context.GetHttpContext();
            var playerId = httpContext?.Items["PlayerId"]?.ToString() ?? Guid.NewGuid().ToString("N");

            await _statsService.GetOrCreatePlayerAsync(playerId, playerName);

            var lobby = Lobbies.GetOrAdd(lobbyCode, _ => new ColorCardLobby
            {
                LobbyCode = lobbyCode,
                CreatorConnectionId = Context.ConnectionId
            });

            var existingPlayer = lobby.Players.FirstOrDefault(p =>
                p.PlayerId.Equals(playerId, StringComparison.OrdinalIgnoreCase));

            if (existingPlayer != null)
            {
                existingPlayer.ConnectionId = Context.ConnectionId;
                existingPlayer.Name = playerName;
                existingPlayer.IsActive = true;
            }
            else
            {
                if (lobby.Players.Count >= 5)
                {
                    await Clients.Caller.SendAsync("Error", "Game is full (max 5 players)");
                    return;
                }

                if (lobby.GameState.Phase != GamePhase.Waiting)
                {
                    await Clients.Caller.SendAsync("Error", "Game already in progress");
                    return;
                }

                var newPlayer = new ColorCardPlayer
                {
                    ConnectionId = Context.ConnectionId,
                    Name = playerName,
                    PlayerId = playerId,
                    SeatPosition = lobby.Players.Count
                };
                lobby.Players.Add(newPlayer);
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, lobbyCode);
            await Clients.Caller.SendAsync("SetConnectionId", Context.ConnectionId);

            var playerNames = lobby.Players.Select(p => p.Name).ToList();
            await Clients.Group(lobbyCode).SendAsync("UpdatePlayerList", playerNames, lobby.CreatorConnectionId);

            if (lobby.GameState.Phase != GamePhase.Waiting)
            {
                await Clients.Caller.SendAsync("GameStarted", lobbyCode);
            }

            await BroadcastGameState(lobbyCode);
        }

        public async Task StartGame(string lobbyCode)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;

            if (Context.ConnectionId != lobby.CreatorConnectionId)
            {
                await Clients.Caller.SendAsync("Error", "Only the host can start the game");
                return;
            }

            if (lobby.Players.Count < 2)
            {
                await Clients.Caller.SendAsync("Error", "Need at least 2 players to start");
                return;
            }

            lobby.IsGameStarted = true;
            await Clients.Group(lobbyCode).SendAsync("GameStarted", lobbyCode);
            await StartNewRound(lobbyCode);
        }

        private async Task StartNewRound(string lobbyCode)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;

            var game = lobby.GameState;

            game.Deck = CreateShuffledDeck();
            game.DiscardPile.Clear();
            game.CurrentPlayerIndex = 0;
            game.IsClockwise = true;
            game.CurrentColor = null;
            game.DrawStackCount = 0;
            game.Phase = GamePhase.Playing;
            game.GameLog.Clear();

            foreach (var player in lobby.Players)
            {
                player.Hand.Clear();
                player.HasCalledUno = false;
            }

            for (int i = 0; i < 7; i++)
            {
                foreach (var player in lobby.Players)
                {
                    player.Hand.Add(DrawCardFromDeck(game));
                }
            }

            ColorCard firstCard;
            do
            {
                firstCard = DrawCardFromDeck(game);
            } while (firstCard.Color == CardColor.Wild || firstCard.Value >= CardValue.Skip);

            game.DiscardPile.Add(firstCard);
            game.CurrentColor = firstCard.Color;

            game.GameLog.Add($"?? Game started! First card: {firstCard.Color} {firstCard.Value}");

            await BroadcastGameState(lobbyCode);
            await NotifyCurrentPlayer(lobbyCode);
        }

        public async Task PlayerAction(string lobbyCode, PlayerAction action)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var game = lobby.GameState;

            var player = lobby.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
            if (player == null || lobby.Players[game.CurrentPlayerIndex] != player) return;

            switch (action.Action.ToLower())
            {
                case "playcard":
                    await HandlePlayCard(lobbyCode, player, action);
                    break;

                case "drawcard":
                    await HandleDrawCard(lobbyCode, player);
                    break;

                case "calluno":
                    player.HasCalledUno = true;
                    game.GameLog.Add($"?? {player.Name} called UNO!");
                    await BroadcastGameState(lobbyCode);
                    break;

                case "choosecolor":
                    if (action.ChosenColor.HasValue)
                    {
                        game.CurrentColor = action.ChosenColor.Value;
                        game.GameLog.Add($"?? {player.Name} chose {action.ChosenColor.Value}");
                        await AdvanceToNextPlayer(lobbyCode);
                    }
                    break;
            }
        }

        private async Task HandlePlayCard(string lobbyCode, ColorCardPlayer player, PlayerAction action)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var game = lobby.GameState;

            if (!action.CardIndex.HasValue || action.CardIndex.Value >= player.Hand.Count)
            {
                await Clients.Caller.SendAsync("Error", "Invalid card");
                return;
            }

            var card = player.Hand[action.CardIndex.Value];
            var topCard = game.TopCard;

            if (!card.CanPlayOn(topCard, game.CurrentColor))
            {
                await Clients.Caller.SendAsync("Error", "Cannot play that card");
                return;
            }

            player.Hand.RemoveAt(action.CardIndex.Value);
            game.DiscardPile.Add(card);
            game.GameLog.Add($"?? {player.Name} played {card.Color} {card.Value}");

            if (player.Hand.Count == 1 && !player.HasCalledUno)
            {
                game.GameLog.Add($"?? {player.Name} forgot to call UNO! Drawing 2 cards.");
                player.Hand.Add(DrawCardFromDeck(game));
                player.Hand.Add(DrawCardFromDeck(game));
            }

            if (player.Hand.Count == 0)
            {
                await EndGame(lobbyCode, player);
                return;
            }

            await HandleSpecialCard(lobbyCode, card);
        }

        private async Task HandleSpecialCard(string lobbyCode, ColorCard card)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var game = lobby.GameState;

            switch (card.Value)
            {
                case CardValue.Skip:
                    game.GameLog.Add("?? Next player skipped!");
                    await AdvanceToNextPlayer(lobbyCode);
                    await AdvanceToNextPlayer(lobbyCode);
                    break;

                case CardValue.Reverse:
                    game.IsClockwise = !game.IsClockwise;
                    game.GameLog.Add("?? Direction reversed!");

                    if (lobby.Players.Count == 2)
                    {
                        await AdvanceToNextPlayer(lobbyCode);
                    }
                    await AdvanceToNextPlayer(lobbyCode);
                    break;

                case CardValue.DrawTwo:
                    game.DrawStackCount += 2;
                    game.GameLog.Add($"? Next player must draw {game.DrawStackCount} cards!");
                    await AdvanceToNextPlayer(lobbyCode);
                    break;

                case CardValue.Wild:
                    await Clients.Client(lobby.Players[game.CurrentPlayerIndex].ConnectionId)
                        .SendAsync("ChooseColorPrompt");
                    await BroadcastGameState(lobbyCode);
                    break;

                case CardValue.WildDrawFour:
                    game.DrawStackCount += 4;
                    game.GameLog.Add($"?? Next player must draw {game.DrawStackCount} cards!");
                    await Clients.Client(lobby.Players[game.CurrentPlayerIndex].ConnectionId)
                        .SendAsync("ChooseColorPrompt");
                    await BroadcastGameState(lobbyCode);
                    await AdvanceToNextPlayer(lobbyCode);
                    break;

                default:
                    game.CurrentColor = card.Color;
                    await AdvanceToNextPlayer(lobbyCode);
                    break;
            }
        }

        private async Task HandleDrawCard(string lobbyCode, ColorCardPlayer player)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var game = lobby.GameState;

            if (game.DrawStackCount > 0)
            {
                for (int i = 0; i < game.DrawStackCount; i++)
                {
                    player.Hand.Add(DrawCardFromDeck(game));
                }
                game.GameLog.Add($"?? {player.Name} drew {game.DrawStackCount} cards");
                game.DrawStackCount = 0;
                await AdvanceToNextPlayer(lobbyCode);
            }
            else
            {
                var drawnCard = DrawCardFromDeck(game);
                player.Hand.Add(drawnCard);
                game.GameLog.Add($"?? {player.Name} drew a card");

                if (drawnCard.CanPlayOn(game.TopCard, game.CurrentColor))
                {
                    await Clients.Client(player.ConnectionId)
                        .SendAsync("CanPlayDrawnCard", player.Hand.Count - 1);
                }
                else
                {
                    await AdvanceToNextPlayer(lobbyCode);
                }
            }

            await BroadcastGameState(lobbyCode);
        }

        private async Task AdvanceToNextPlayer(string lobbyCode)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var game = lobby.GameState;

            int step = game.IsClockwise ? 1 : -1;
            game.CurrentPlayerIndex = (game.CurrentPlayerIndex + step + lobby.Players.Count) % lobby.Players.Count;

            int attempts = 0;
            while (!lobby.Players[game.CurrentPlayerIndex].IsActive && attempts < lobby.Players.Count)
            {
                game.CurrentPlayerIndex = (game.CurrentPlayerIndex + step + lobby.Players.Count) % lobby.Players.Count;
                attempts++;
            }

            await BroadcastGameState(lobbyCode);
            await NotifyCurrentPlayer(lobbyCode);
        }

        private async Task EndGame(string lobbyCode, ColorCardPlayer winner)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var game = lobby.GameState;

            game.Phase = GamePhase.GameOver;
            game.GameLog.Add($"?? {winner.Name} wins!");

            await _statsService.RecordGameResultAsync(winner.PlayerId, true);

            foreach (var player in lobby.Players.Where(p => p.PlayerId != winner.PlayerId))
            {
                await _statsService.RecordGameResultAsync(player.PlayerId, false);
            }

            var result = new GameResult
            {
                WinnerName = winner.Name,
                WinnerId = winner.PlayerId,
                FinalScores = lobby.Players.Select(p => new PlayerScore
                {
                    Name = p.Name,
                    CardsLeft = p.Hand.Count,
                    Points = p.Hand.Sum(c => c.GetPoints())
                }).OrderBy(s => s.CardsLeft).ToList()
            };

            await Clients.Group(lobbyCode).SendAsync("GameOver", result);

            await Task.Delay(5000);
            await StartNewRound(lobbyCode);
        }

        private async Task BroadcastGameState(string lobbyCode)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var game = lobby.GameState;

            foreach (var player in lobby.Players)
            {
                await Clients.Client(player.ConnectionId).SendAsync("GameState", new
                {
                    players = lobby.Players.Select(p => new
                    {
                        name = p.Name,
                        cardCount = p.Hand.Count,
                        hasCalledUno = p.HasCalledUno,
                        seatPosition = p.SeatPosition,
                        isActive = p.IsActive
                    }).ToList(),
                    topCard = game.TopCard?.ToString(),
                    currentColor = game.CurrentColor?.ToString(),
                    deckCount = game.Deck.Count,
                    currentPlayerIndex = game.CurrentPlayerIndex,
                    isClockwise = game.IsClockwise,
                    phase = game.Phase.ToString(),
                    gameLog = game.GameLog.TakeLast(5).ToList(),
                    myHand = player.Hand.Select((c, i) => new { index = i, card = c.ToString() }).ToList(),
                    isMyTurn = game.CurrentPlayerIndex == player.SeatPosition,
                    isCreator = player.ConnectionId == lobby.CreatorConnectionId
                });
            }
        }

        private async Task NotifyCurrentPlayer(string lobbyCode)
        {
            if (!Lobbies.TryGetValue(lobbyCode, out var lobby)) return;
            var currentPlayer = lobby.Players[lobby.GameState.CurrentPlayerIndex];

            if (!currentPlayer.IsActive)
            {
                await HandleDrawCard(lobbyCode, currentPlayer);
                return;
            }

            await Clients.Client(currentPlayer.ConnectionId).SendAsync("YourTurn");
        }

        private List<ColorCard> CreateShuffledDeck()
        {
            var deck = new List<ColorCard>();
            var colors = new[] { CardColor.Red, CardColor.Blue, CardColor.Green, CardColor.Yellow };

            foreach (var color in colors)
            {
                deck.Add(new ColorCard { Color = color, Value = CardValue.Zero });

                for (int i = 0; i < 2; i++)
                {
                    for (int val = 1; val <= 9; val++)
                    {
                        deck.Add(new ColorCard { Color = color, Value = (CardValue)val });
                    }
                    deck.Add(new ColorCard { Color = color, Value = CardValue.Skip });
                    deck.Add(new ColorCard { Color = color, Value = CardValue.Reverse });
                    deck.Add(new ColorCard { Color = color, Value = CardValue.DrawTwo });
                }
            }

            for (int i = 0; i < 4; i++)
            {
                deck.Add(new ColorCard { Color = CardColor.Wild, Value = CardValue.Wild });
                deck.Add(new ColorCard { Color = CardColor.Wild, Value = CardValue.WildDrawFour });
            }

            return deck.OrderBy(_ => Random.Shared.Next()).ToList();
        }

        private ColorCard DrawCardFromDeck(ColorCardGameState game)
        {
            if (game.Deck.Count == 0)
            {
                var topCard = game.DiscardPile[^1];
                game.DiscardPile.RemoveAt(game.DiscardPile.Count - 1);
                game.Deck = game.DiscardPile.OrderBy(_ => Random.Shared.Next()).ToList();
                game.DiscardPile.Clear();
                game.DiscardPile.Add(topCard);
            }

            var card = game.Deck[0];
            game.Deck.RemoveAt(0);
            return card;
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            foreach (var lobby in Lobbies.Values)
            {
                var player = lobby.Players.FirstOrDefault(p => p.ConnectionId == Context.ConnectionId);
                if (player != null)
                {
                    player.IsActive = false;
                    await Clients.Group(lobby.LobbyCode).SendAsync("PlayerDisconnected", player.Name);
                }
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}