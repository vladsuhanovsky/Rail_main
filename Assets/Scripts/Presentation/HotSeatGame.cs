using System;
using System.Collections.Generic;
using System.Linq;
using RailGame.Core;
using RailGame.Presentation.Ui;
using UnityEngine;

namespace RailGame.Presentation
{
    [Serializable]
    public class PlayerSlot
    {
        public string name = "Игрок";
        public bool isBot;
        [Tooltip("Smart — строит пути под задания; Random — случайные ходы.")]
        public BotLevel botLevel = BotLevel.Smart;

        public PlayerSlot() { }

        public PlayerSlot(string name, bool isBot, BotLevel botLevel = BotLevel.Smart)
        {
            this.name = name;
            this.isBot = isBot;
            this.botLevel = botLevel;
        }
    }

    /// <summary>
    /// Играбельный прототип: карта, рука, открытые карты, задания, журнал.
    /// Игра за одним компьютером (hot-seat), любой игрок может быть ботом.
    /// Всё рисуется через IMGUI (OnGUI): не нужны ни префабы, ни Canvas,
    /// и работает с любой системой ввода. Повесьте компонент на пустой объект и нажмите Play.
    /// </summary>
    public sealed class HotSeatGame : MonoBehaviour
    {
        [Header("Партия")]
        [SerializeField] string mapResourcePath = "Maps/demo_map";
        [SerializeField] PlayerSlot[] players = { new PlayerSlot("Аня", false), new PlayerSlot("Борис", true) };
        [SerializeField] int trainsPerPlayer = 30;
        [SerializeField] bool randomSeed = true;
        [SerializeField] int seed = 1;

        [Header("Интерфейс")]
        [Tooltip("Если людей больше одного, перед каждым ходом карты скрываются, пока игрок не нажмёт «Показать».")]
        [SerializeField] bool hideCardsBetweenTurns = true;
        [SerializeField] float botTurnDelay = 0.7f;

        // Интерфейс рисуется в «виртуальных» пикселях высотой 1080 и масштабируется под экран.
        const float ReferenceHeight = 1080f;
        const float SidePanelWidth = 380f;
        const float BottomPanelHeight = 190f;
        const int LogLines = 9;

        Game _game;
        BoardLayout _layout;
        IBot[] _bots;
        string _fatalError;
        bool _restartRequested;

        readonly List<string> _log = new List<string>();
        string _flash;
        float _flashUntil;

        float _scale = 1f;
        float _width;
        Vector2 _mouse;
        Route _hoverRoute;
        Ticket _hoverTicket;

        Route _paymentRoute;
        IReadOnlyList<ClaimOption> _paymentOptions;
        Vector2 _paymentAnchor;

        readonly HashSet<string> _selectedTickets = new HashSet<string>();
        string _selectionKey;

        int _revealedFor = -1;
        float _botTimer;

        GUIStyle _text, _textSmall, _textDim, _title, _big, _centered, _cityLabel, _mapTitle,
            _cardLabel, _cardCount, _button, _rightSmall;

        // ================================================================== Жизненный цикл

        void Start() => NewGame();

        void NewGame()
        {
            try
            {
                var map = MapLoader.LoadFromResources(mapResourcePath);
                var names = players.Select(p => p.name).ToList();
                int actualSeed = randomSeed ? UnityEngine.Random.Range(0, int.MaxValue) : seed;

                _game = new Game(map, names, new GameConfig { TrainsPerPlayer = trainsPerPlayer }, actualSeed);
                _game.Log += AddLog;
                _layout = new BoardLayout(map);
                _bots = new IBot[players.Length];
                for (int i = 0; i < players.Length; i++)
                    if (players[i].isBot) _bots[i] = Bots.Create(players[i].botLevel, actualSeed + i);

                _log.Clear();
                AddLog($"Новая партия на карте «{map.Name}». Seed: {actualSeed}.");
                _flash = null;
                _paymentRoute = null;
                _selectionKey = null;
                _selectedTickets.Clear();
                _revealedFor = -1;
                _botTimer = 0f;
                _fatalError = null;
            }
            catch (Exception e)
            {
                _game = null;
                _fatalError = e.Message;
                Debug.LogException(e);
            }
        }

        void Update()
        {
            if (_restartRequested)
            {
                _restartRequested = false;
                NewGame();
                return;
            }

            if (_game == null || _game.Phase == GamePhase.GameOver) return;

            int active = ActivePlayer;
            if (!IsBot(active))
            {
                _botTimer = 0f;
                return;
            }

            _botTimer += Time.deltaTime;
            if (_botTimer < botTurnDelay) return;
            _botTimer = 0f;

            var result = _bots[active].Act(_game);
            if (!result.Success) Flash("Бот ошибся: " + result.Error);
        }

        // ================================================================== Кто ходит и кто смотрит

        /// <summary>Игрок, от которого сейчас ждут действия (на старте — тот, кто выбирает задания).</summary>
        int ActivePlayer
        {
            get
            {
                return _game == null ? -1 : _game.ActingPlayerIndex;
            }
        }

        bool IsBot(int index) => index >= 0 && index < players.Length && players[index].isBot;

        int HumanCount => players.Count(p => !p.isBot);

        bool NeedsHandoff
        {
            get
            {
                int active = ActivePlayer;
                return hideCardsBetweenTurns && HumanCount > 1 && active >= 0 && !IsBot(active) && _revealedFor != active;
            }
        }

        /// <summary>Человек, чья рука и задания показаны на экране.</summary>
        int Viewer
        {
            get
            {
                int active = ActivePlayer;
                if (active >= 0 && !IsBot(active)) return active;
                if (_revealedFor >= 0 && !IsBot(_revealedFor)) return _revealedFor;
                for (int i = 0; i < _game.Players.Count; i++)
                    if (!IsBot(i)) return i;
                return Math.Max(active, 0);
            }
        }

        bool HumanToAct
        {
            get
            {
                int active = ActivePlayer;
                return active >= 0 && !IsBot(active) && !NeedsHandoff;
            }
        }

        // ================================================================== Отрисовка

        void OnGUI()
        {
            EnsureStyles();
            _scale = Screen.height / ReferenceHeight;
            _mouse = Event.current.mousePosition / _scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(_scale, _scale, 1f));
            _width = Screen.width / _scale;

            if (_fatalError != null)
            {
                DrawFatal();
                return;
            }
            if (_game == null) return;

            var mapRect = new Rect(0f, 0f, _width - SidePanelWidth, ReferenceHeight - BottomPanelHeight);
            var sideRect = new Rect(_width - SidePanelWidth, 0f, SidePanelWidth, ReferenceHeight);
            var bottomRect = new Rect(0f, ReferenceHeight - BottomPanelHeight, _width - SidePanelWidth, BottomPanelHeight);

            bool gameOver = _game.Phase == GamePhase.GameOver;
            bool handoff = NeedsHandoff;
            if (_paymentRoute != null && (!HumanToAct || _game.Phase != GamePhase.AwaitingAction)) _paymentRoute = null;

            _layout.Fit(mapRect);
            _hoverRoute = !handoff && !gameOver && _paymentRoute == null && mapRect.Contains(_mouse)
                ? _layout.RouteAt(_mouse)
                : null;

            // Панели рисуем первыми: при наведении на задание они запоминают его, а карта подсвечивает города.
            _hoverTicket = null;
            GUI.enabled = !handoff && !gameOver && _paymentRoute == null;
            DrawSidePanel(sideRect, handoff);
            DrawBottomPanel(bottomRect, handoff);
            GUI.enabled = true;

            DrawMap(mapRect);
            if (_hoverRoute != null) DrawRouteTooltip(_hoverRoute);
            if (_paymentRoute != null) DrawPaymentPopup(mapRect);
            if (handoff) DrawHandoff();
            if (gameOver) DrawGameOver();

            HandleMapClick();
        }

        void DrawMap(Rect rect)
        {
            GuiDraw.Rect(rect, Palette.MapBackground);
            GUI.Label(new Rect(rect.x + 18f, rect.y + 10f, 600f, 30f), _game.Map.Name, _mapTitle);

            foreach (var route in _game.Map.Routes)
            {
                int? owner = _game.GetRouteOwner(route.Id);
                bool closed = !owner.HasValue && IsClosedParallel(route);
                bool hovered = route == _hoverRoute || route == _paymentRoute;

                var fill = owner.HasValue ? Palette.PlayerColor(owner.Value) : Palette.RouteColor(route);
                var outline = hovered ? Palette.Highlight : Palette.TrackOutline;
                if (closed)
                {
                    fill.a = 0.25f;
                    outline.a = 0.25f;
                }
                float border = hovered ? 6f : 3f;

                foreach (var segment in _layout.GetSegments(route))
                {
                    GuiDraw.RotatedRect(segment.Center, segment.Length + border, BoardLayout.TrackThickness + border,
                        segment.Angle, outline, 3f);
                    GuiDraw.RotatedRect(segment.Center, segment.Length, BoardLayout.TrackThickness, segment.Angle, fill, 2f);
                    if (owner.HasValue) // «вагончик» на занятом пути
                        GuiDraw.RotatedRect(segment.Center, segment.Length * 0.55f, BoardLayout.TrackThickness * 0.35f,
                            segment.Angle, new Color(0f, 0f, 0f, 0.35f), 1f);
                }
            }

            foreach (var city in _game.Map.Cities)
            {
                var position = _layout.CityPosition(city.Id);
                if (_hoverTicket != null && (_hoverTicket.CityA == city.Id || _hoverTicket.CityB == city.Id))
                    GuiDraw.Circle(position, BoardLayout.CityRadius + 9f, Palette.Highlight);

                GuiDraw.Circle(position, BoardLayout.CityRadius + 2.5f, Palette.TrackOutline);
                GuiDraw.Circle(position, BoardLayout.CityRadius, Palette.CityFill);

                var size = _cityLabel.CalcSize(new GUIContent(city.Name));
                var labelRect = new Rect(position.x - size.x / 2f - 4f, position.y + BoardLayout.CityRadius + 3f, size.x + 8f, size.y);
                GuiDraw.Rect(labelRect, new Color(0.93f, 0.90f, 0.82f, 0.85f), 4f);
                GUI.Label(labelRect, city.Name, _cityLabel);
            }
        }

        void DrawRouteTooltip(Route route)
        {
            string colorName = route.Color.HasValue ? Palette.CardTitle(route.Color.Value) : "Серый (любой один цвет)";
            string text = $"{CityName(route.CityA)} — {CityName(route.CityB)}\n" +
                          $"Длина {route.Length} · {colorName} · {_game.Config.PointsForRouteLength(route.Length)} очк.";

            int? owner = _game.GetRouteOwner(route.Id);
            if (owner.HasValue) text += "\nЗанят: " + _game.Players[owner.Value].Name;
            else if (IsClosedParallel(route)) text += "\nЗакрыт: параллельный путь уже занят";
            else if (HumanToAct && _game.Phase == GamePhase.AwaitingAction)
            {
                string blocker = _game.GetClaimBlocker(route.Id);
                text += "\n" + (blocker ?? "Кликните, чтобы занять");
            }

            const float width = 300f;
            float height = _textSmall.CalcHeight(new GUIContent(text), width - 20f) + 16f;
            float x = Mathf.Min(_mouse.x + 18f, _width - SidePanelWidth - width - 8f);
            float y = Mathf.Min(_mouse.y + 18f, ReferenceHeight - BottomPanelHeight - height - 8f);
            var rect = new Rect(x, y, width, height);

            GuiDraw.Rect(rect, new Color(0.10f, 0.11f, 0.14f, 0.95f), 6f);
            GUI.Label(new Rect(rect.x + 10f, rect.y + 8f, width - 20f, height - 16f), text, _textSmall);
        }

        void DrawPaymentPopup(Rect mapRect)
        {
            const float width = 340f;
            float height = 58f + 40f * (_paymentOptions.Count + 1);
            var rect = new Rect(
                Mathf.Min(_paymentAnchor.x + 12f, mapRect.xMax - width - 10f),
                Mathf.Min(_paymentAnchor.y + 12f, mapRect.yMax - height - 10f),
                width, height);

            GuiDraw.Rect(rect, Palette.PanelBackground, 8f);
            GuiDraw.Frame(rect, Palette.Highlight, 2f, 8f);
            GUI.Label(new Rect(rect.x + 14f, rect.y + 12f, width - 28f, 30f),
                $"Чем платить: {CityName(_paymentRoute.CityA)} — {CityName(_paymentRoute.CityB)}?", _text);

            float y = rect.y + 50f;
            foreach (var option in _paymentOptions)
            {
                if (GUI.Button(new Rect(rect.x + 14f, y, width - 28f, 34f), PaymentLabel(_paymentRoute, option), _button))
                {
                    var route = _paymentRoute;
                    _paymentRoute = null;
                    Apply(_game.ClaimRoute(route.Id, option.Color, option.Locomotives));
                    return;
                }
                y += 40f;
            }

            if (GUI.Button(new Rect(rect.x + 14f, y, width - 28f, 34f), "Отмена", _button))
                _paymentRoute = null;
        }

        void DrawSidePanel(Rect rect, bool hidden)
        {
            GuiDraw.Rect(rect, Palette.PanelBackground);
            float x = rect.x + 16f;
            float w = rect.width - 32f;
            float y = rect.y + 14f;

            // --- Открытые карты, колода, задания
            GUI.Label(new Rect(x, y, w, 26f), "Открытые карты", _title);
            y += 32f;

            bool canDraw = HumanToAct &&
                           (_game.Phase == GamePhase.AwaitingAction || _game.Phase == GamePhase.DrawingSecondCard);
            int slots = _game.FaceUpCards.Count;
            float cardWidth = (w - 8f * (slots - 1)) / slots;
            const float cardHeight = 84f;

            for (int i = 0; i < slots; i++)
            {
                var cardRect = new Rect(x + i * (cardWidth + 8f), y, cardWidth, cardHeight);
                var card = _game.FaceUpCards[i];
                if (!card.HasValue)
                {
                    GuiDraw.Frame(cardRect, Palette.PanelTextDim, 2f, 6f);
                    continue;
                }

                bool forbidden = _game.Phase == GamePhase.DrawingSecondCard && card.Value == CardColor.Locomotive;
                DrawCard(cardRect, card.Value, null, !canDraw || forbidden);
                if (GuiDraw.HitArea(cardRect))
                {
                    int slot = i;
                    Act(() => _game.DrawFaceUpCard(slot));
                }
            }
            y += cardHeight + 12f;

            float half = (w - 10f) / 2f;
            if (GUI.Button(new Rect(x, y, half, 40f), $"Из колоды ({_game.DeckCount})", _button))
                Act(_game.DrawCardFromDeck);
            if (GUI.Button(new Rect(x + half + 10f, y, half, 40f), $"Задания ({_game.TicketDeckCount})", _button))
                Act(_game.DrawTickets);
            y += 56f;

            // --- Игроки
            GUI.Label(new Rect(x, y, w, 26f), "Игроки", _title);
            y += 32f;
            foreach (var player in _game.Players)
            {
                DrawPlayerRow(new Rect(x, y, w, 46f), player);
                y += 52f;
            }
            y += 8f;

            // --- Задания и журнал
            float logTop = rect.yMax - 16f - LogLines * 20f - 30f;
            var ticketRect = new Rect(x, y, w, logTop - y - 12f);
            if (TryGetTicketChooser(out var chooser)) DrawTicketChoice(ticketRect, chooser);
            else DrawTicketList(ticketRect, hidden);

            DrawLog(new Rect(x, logTop, w, rect.yMax - logTop - 12f));
        }

        void DrawPlayerRow(Rect rect, PlayerState player)
        {
            bool active = player.Index == ActivePlayer;
            var color = Palette.PlayerColor(player.Index);

            GuiDraw.Rect(rect, Palette.PanelSection, 6f);
            if (active) GuiDraw.Frame(rect, color, 2f, 6f);
            GuiDraw.Rect(new Rect(rect.x + 10f, rect.y + 13f, 20f, 20f), color, 4f);

            string name = player.Name + BotTag(player.Index) + (active ? " — ходит" : "");
            GUI.Label(new Rect(rect.x + 40f, rect.y + 3f, rect.width - 48f, 22f), name, _text);
            GUI.Label(new Rect(rect.x + 40f, rect.y + 23f, rect.width - 48f, 20f),
                $"Очки {player.RoutePoints} · вагоны {player.TrainsLeft} · карты {player.HandSize} · задания {player.Tickets.Count}",
                _textDim);
        }

        bool TryGetTicketChooser(out PlayerState chooser)
        {
            chooser = null;
            if (!HumanToAct) return false;
            if (_game.Phase != GamePhase.SetupTickets && _game.Phase != GamePhase.ChoosingTickets) return false;

            var player = _game.Players[ActivePlayer];
            if (player.PendingTickets.Count == 0) return false;
            chooser = player;
            return true;
        }

        void DrawTicketChoice(Rect rect, PlayerState player)
        {
            bool setup = _game.Phase == GamePhase.SetupTickets;
            int minKeep = setup ? _game.Config.InitialTicketsMinKeep : _game.Config.TicketsMinKeep;
            int required = Math.Min(minKeep, player.PendingTickets.Count);

            // Новый набор заданий — сбрасываем выбор. На старте по умолчанию выбраны все.
            string key = player.Index + ":" + string.Join(",", player.PendingTickets.Select(t => t.Id));
            if (key != _selectionKey)
            {
                _selectionKey = key;
                _selectedTickets.Clear();
                if (setup) foreach (var t in player.PendingTickets) _selectedTickets.Add(t.Id);
            }

            GuiDraw.Rect(rect, Palette.PanelSection, 8f);
            GuiDraw.Frame(rect, Palette.Highlight, 2f, 8f);
            float x = rect.x + 12f;
            float w = rect.width - 24f;
            float y = rect.y + 10f;

            GUI.Label(new Rect(x, y, w, 26f), setup ? "Стартовые задания" : "Новые задания", _title);
            y += 28f;
            GUI.Label(new Rect(x, y, w, 40f),
                $"Оставьте хотя бы {required}. Наведите — города подсветятся. Невыполненное задание в конце отнимает очки.",
                _textDim);
            y += 46f;

            foreach (var ticket in player.PendingTickets)
            {
                var row = new Rect(x, y, w, 38f);
                bool selected = _selectedTickets.Contains(ticket.Id);
                if (row.Contains(_mouse)) _hoverTicket = ticket;

                GuiDraw.Rect(row, selected ? new Color(0.30f, 0.36f, 0.24f) : Palette.PanelBackground, 6f);
                var box = new Rect(row.x + 10f, row.y + 10f, 18f, 18f);
                GuiDraw.Frame(box, Palette.PanelText, 2f, 3f);
                if (selected) GuiDraw.Rect(new Rect(box.x + 4f, box.y + 4f, 10f, 10f), Palette.Success, 2f);

                GUI.Label(new Rect(row.x + 38f, row.y + 9f, row.width - 90f, 22f),
                    $"{CityName(ticket.CityA)} — {CityName(ticket.CityB)}", _text);
                GUI.Label(new Rect(row.xMax - 60f, row.y + 9f, 50f, 22f), ticket.Points.ToString(), _rightSmall);

                if (GuiDraw.HitArea(row))
                {
                    if (selected) _selectedTickets.Remove(ticket.Id);
                    else _selectedTickets.Add(ticket.Id);
                }
                y += 44f;
            }

            y += 4f;
            if (GUI.Button(new Rect(x, y, w, 40f), $"Оставить выбранные ({_selectedTickets.Count})", _button))
            {
                var ids = _selectedTickets.ToList();
                if (ids.Count < required) Flash($"Нужно оставить хотя бы {required}.");
                else Apply(setup ? _game.KeepInitialTickets(player.Index, ids) : _game.KeepTickets(ids));
            }
        }

        void DrawTicketList(Rect rect, bool hidden)
        {
            var viewer = _game.Players[Viewer];
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 26f), "Задания: " + viewer.Name, _title);
            float y = rect.y + 32f;

            if (hidden)
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 24f), "Скрыто до передачи хода.", _textDim);
                return;
            }
            if (viewer.Tickets.Count == 0)
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 24f), "Заданий пока нет.", _textDim);
                return;
            }

            int capacity = Mathf.Max(1, Mathf.FloorToInt((rect.yMax - y) / 34f));
            int shown = 0;
            foreach (var ticket in viewer.Tickets)
            {
                if (shown == capacity - 1 && viewer.Tickets.Count > capacity)
                {
                    GUI.Label(new Rect(rect.x, y + 6f, rect.width, 22f),
                        $"…и ещё {viewer.Tickets.Count - shown}", _textDim);
                    break;
                }

                bool done = Scoring.IsTicketCompleted(ticket, viewer.ClaimedRoutes);
                var row = new Rect(rect.x, y, rect.width, 30f);
                if (row.Contains(_mouse)) _hoverTicket = ticket;

                GuiDraw.Rect(row, row.Contains(_mouse) ? new Color(0.28f, 0.31f, 0.37f) : Palette.PanelSection, 6f);
                var mark = new Vector2(row.x + 16f, row.center.y);
                if (done) GuiDraw.Circle(mark, 7f, Palette.Success);
                else GuiDraw.Ring(mark, 7f, 2f, Palette.PanelTextDim);

                GUI.Label(new Rect(row.x + 32f, row.y + 5f, row.width - 80f, 22f),
                    $"{CityName(ticket.CityA)} — {CityName(ticket.CityB)}", _textSmall);
                GUI.Label(new Rect(row.xMax - 60f, row.y + 5f, 50f, 22f),
                    (done ? "+" : "") + ticket.Points, _rightSmall);

                y += 34f;
                shown++;
            }
        }

        void DrawLog(Rect rect)
        {
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 26f), "Журнал", _title);
            float y = rect.y + 30f;
            int start = Math.Max(0, _log.Count - LogLines);
            for (int i = start; i < _log.Count; i++)
            {
                GUI.Label(new Rect(rect.x, y, rect.width, 20f), _log[i], _textDim);
                y += 20f;
            }
        }

        void DrawBottomPanel(Rect rect, bool hidden)
        {
            GuiDraw.Rect(rect, Palette.PanelBackground);
            GuiDraw.Rect(new Rect(rect.x, rect.y, rect.width, 2f), Palette.PanelSection);

            const float statusWidth = 400f;
            DrawStatus(new Rect(rect.x + 16f, rect.y + 12f, statusWidth - 32f, rect.height - 24f));
            DrawHand(new Rect(rect.x + statusWidth, rect.y + 12f, rect.width - statusWidth - 16f, rect.height - 24f), hidden);
        }

        void DrawStatus(Rect rect)
        {
            int active = ActivePlayer;
            if (active >= 0)
            {
                var player = _game.Players[active];
                GuiDraw.Rect(new Rect(rect.x, rect.y + 4f, 20f, 20f), Palette.PlayerColor(active), 4f);
                GUI.Label(new Rect(rect.x + 30f, rect.y, rect.width - 30f, 28f),
                    player.Name + BotTag(active), _title);
            }

            GUI.Label(new Rect(rect.x, rect.y + 32f, rect.width, 56f), Instruction(active), _text);

            if (_game.IsFinalRound)
            {
                var style = new GUIStyle(_text);
                style.normal.textColor = Palette.Highlight;
                GUI.Label(new Rect(rect.x, rect.y + 88f, rect.width, 22f), "Последний круг!", style);
            }

            if (_flash != null && Time.time < _flashUntil)
            {
                var style = new GUIStyle(_textSmall);
                style.normal.textColor = Palette.Error;
                GUI.Label(new Rect(rect.x, rect.y + 110f, rect.width, 40f), _flash, style);
            }

            if (HumanToAct && _game.Phase == GamePhase.AwaitingAction && !_game.HasAnyLegalAction())
            {
                if (GUI.Button(new Rect(rect.x, rect.yMax - 34f, 220f, 34f), "Пропустить ход", _button))
                    Act(_game.Pass);
            }
        }

        string Instruction(int active)
        {
            if (active < 0) return "Партия окончена.";
            if (IsBot(active)) return "Ходит бот…";
            if (NeedsHandoff) return "Передайте компьютер этому игроку.";

            switch (_game.Phase)
            {
                case GamePhase.SetupTickets:
                    return "Выберите стартовые задания на панели справа.";
                case GamePhase.AwaitingAction:
                    return "Возьмите 2 карты, займите маршрут кликом по карте или возьмите задания.";
                case GamePhase.DrawingSecondCard:
                    return "Возьмите вторую карту. Открытый локомотив сейчас брать нельзя.";
                case GamePhase.ChoosingTickets:
                    return "Выберите справа, какие задания оставить.";
                default:
                    return "";
            }
        }

        void DrawHand(Rect rect, bool hidden)
        {
            var viewer = _game.Players[Viewer];
            GUI.Label(new Rect(rect.x, rect.y, rect.width, 26f),
                hidden ? "Карты скрыты" : $"Рука: {viewer.Name} · вагонов осталось {viewer.TrainsLeft}", _title);
            if (hidden) return;

            const float cardWidth = 70f;
            const float gap = 8f;
            float cardHeight = rect.height - 34f;
            int i = 0;
            foreach (var color in CardColors.Regular.Concat(new[] { CardColor.Locomotive }))
            {
                int count = viewer.CardCount(color);
                DrawCard(new Rect(rect.x + i * (cardWidth + gap), rect.y + 32f, cardWidth, cardHeight), color, count, count == 0);
                i++;
            }
        }

        void DrawCard(Rect rect, CardColor color, int? count, bool dim)
        {
            var fill = Palette.Card(color);
            GuiDraw.Rect(rect, fill, 6f);
            GuiDraw.Frame(rect, new Color(0f, 0f, 0f, 0.5f), 2f, 6f);

            if (color == CardColor.Locomotive)
            {
                // Радужная полоса — чтобы локомотив сразу отличался от цветных карт.
                var stripes = new[] { CardColor.Red, CardColor.Orange, CardColor.Yellow, CardColor.Green, CardColor.Blue, CardColor.Purple };
                float stripeWidth = (rect.width - 12f) / stripes.Length;
                for (int i = 0; i < stripes.Length; i++)
                    GuiDraw.Rect(new Rect(rect.x + 6f + i * stripeWidth, rect.yMax - 18f, stripeWidth, 8f), Palette.Card(stripes[i]));
            }

            var textColor = Palette.TextOn(fill);
            _cardLabel.normal.textColor = textColor;
            _cardCount.normal.textColor = textColor;
            GUI.Label(new Rect(rect.x + 2f, rect.y + 5f, rect.width - 4f, 18f), Palette.CardTitle(color), _cardLabel);
            if (count.HasValue)
                GUI.Label(new Rect(rect.x, rect.y + 18f, rect.width, rect.height - 30f), count.Value.ToString(), _cardCount);

            if (dim) GuiDraw.Rect(rect, new Color(0.10f, 0.10f, 0.12f, 0.6f), 6f);
        }

        void DrawHandoff()
        {
            GuiDraw.Rect(new Rect(0f, 0f, _width, ReferenceHeight), Palette.Overlay);
            var player = _game.Players[ActivePlayer];
            var box = new Rect(_width / 2f - 280f, ReferenceHeight / 2f - 130f, 560f, 260f);

            GuiDraw.Rect(box, Palette.PanelBackground, 12f);
            GuiDraw.Frame(box, Palette.PlayerColor(player.Index), 3f, 12f);
            GUI.Label(new Rect(box.x, box.y + 34f, box.width, 44f), "Ход игрока " + player.Name, _big);
            GUI.Label(new Rect(box.x + 30f, box.y + 96f, box.width - 60f, 50f),
                "Остальные — отвернитесь: сейчас откроются карты и задания.", _centered);

            if (GUI.Button(new Rect(box.x + 130f, box.yMax - 82f, 300f, 50f), "Показать мои карты", _button))
                _revealedFor = player.Index;
        }

        void DrawGameOver()
        {
            GuiDraw.Rect(new Rect(0f, 0f, _width, ReferenceHeight), new Color(0.05f, 0.06f, 0.08f, 0.75f));
            var result = _game.Result;
            int rows = result.Ranking.Count;
            float height = 210f + rows * 44f;
            var box = new Rect(_width / 2f - 400f, ReferenceHeight / 2f - height / 2f, 800f, height);

            GuiDraw.Rect(box, Palette.PanelBackground, 12f);
            GuiDraw.Frame(box, Palette.Highlight, 3f, 12f);
            GUI.Label(new Rect(box.x, box.y + 22f, box.width, 44f),
                "Победа: " + string.Join(", ", result.Winners.Select(w => w.PlayerName)), _big);

            float[] columns = { 30f, 300f, 420f, 560f, 680f };
            string[] headers = { "Игрок", "Маршруты", "Задания", "Путь", "Итого" };
            float y = box.y + 86f;
            for (int c = 0; c < headers.Length; c++)
                GUI.Label(new Rect(box.x + columns[c], y, 140f, 24f), headers[c], _textDim);
            y += 30f;

            int place = 1;
            foreach (var s in result.Ranking)
            {
                GuiDraw.Rect(new Rect(box.x + 20f, y - 4f, box.width - 40f, 38f), Palette.PanelSection, 6f);
                GuiDraw.Rect(new Rect(box.x + columns[0], y + 6f, 18f, 18f), Palette.PlayerColor(s.PlayerIndex), 4f);
                GUI.Label(new Rect(box.x + columns[0] + 26f, y + 2f, 260f, 26f), $"{place++}. {s.PlayerName}", _text);
                GUI.Label(new Rect(box.x + columns[1], y + 2f, 120f, 26f), s.RoutePoints.ToString(), _text);
                GUI.Label(new Rect(box.x + columns[2], y + 2f, 140f, 26f),
                    $"{s.TicketPoints} ({s.CompletedTickets}/{s.CompletedTickets + s.FailedTickets})", _text);
                GUI.Label(new Rect(box.x + columns[3], y + 2f, 120f, 26f),
                    s.LongestPath + (s.LongestPathBonus > 0 ? $" (+{s.LongestPathBonus})" : ""), _text);
                GUI.Label(new Rect(box.x + columns[4], y + 2f, 100f, 26f), s.Total.ToString(), _title);
                y += 44f;
            }

            if (GUI.Button(new Rect(box.x + box.width / 2f - 130f, box.yMax - 66f, 260f, 46f), "Новая партия", _button))
                _restartRequested = true;
        }

        void DrawFatal()
        {
            GuiDraw.Rect(new Rect(0f, 0f, _width, ReferenceHeight), Palette.PanelBackground);
            GUI.Label(new Rect(60f, 60f, _width - 120f, 40f), "Не удалось начать партию", _big);
            GUI.Label(new Rect(60f, 130f, _width - 120f, ReferenceHeight - 200f), _fatalError, _text);
        }

        // ================================================================== Ввод

        void HandleMapClick()
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (NeedsHandoff || _game.Phase == GamePhase.GameOver) return;

            if (_paymentRoute != null) // клик мимо окна оплаты закрывает его
            {
                _paymentRoute = null;
                e.Use();
                return;
            }
            if (_hoverRoute == null) return;

            e.Use();
            OnRouteClicked(_hoverRoute);
        }

        void OnRouteClicked(Route route)
        {
            if (!HumanToAct)
            {
                Flash("Сейчас ходит бот.");
                return;
            }

            switch (_game.Phase)
            {
                case GamePhase.SetupTickets:
                    Flash("Сначала выберите стартовые задания.");
                    return;
                case GamePhase.DrawingSecondCard:
                    Flash("Сначала возьмите вторую карту.");
                    return;
                case GamePhase.ChoosingTickets:
                    Flash("Сначала выберите задания.");
                    return;
            }

            var options = _game.GetClaimOptions(route.Id);
            if (options.Count == 0)
            {
                Flash(_game.GetClaimBlocker(route.Id) ?? "Этот маршрут сейчас занять нельзя.");
                return;
            }
            if (options.Count == 1)
            {
                Apply(_game.ClaimRoute(route.Id, options[0].Color, options[0].Locomotives));
                return;
            }

            _paymentRoute = route;
            _paymentOptions = options;
            _paymentAnchor = _mouse;
        }

        // ================================================================== Вспомогательное

        /// <summary>Действие с панели: только для человека, чей сейчас ход.</summary>
        void Act(Func<ActionResult> action)
        {
            if (!HumanToAct)
            {
                Flash(IsBot(ActivePlayer) ? "Сейчас ходит бот." : "Сейчас нельзя.");
                return;
            }
            Apply(action());
        }

        void Apply(ActionResult result)
        {
            if (!result.Success) Flash(result.Error);
        }

        void Flash(string message)
        {
            _flash = message;
            _flashUntil = Time.time + 3.5f;
        }

        void AddLog(string message)
        {
            _log.Add(message);
            if (_log.Count > 200) _log.RemoveAt(0);
        }

        string BotTag(int index)
        {
            if (!IsBot(index)) return "";
            return players[index].botLevel == BotLevel.Random ? " (случайный бот)" : " (бот)";
        }

        bool IsClosedParallel(Route route) =>
            route.ParallelRouteId != null
            && _game.GetRouteOwner(route.ParallelRouteId).HasValue
            && _game.Players.Count < _game.Config.DoubleRoutesMinPlayers;

        string CityName(string cityId) => _game.Map.GetCity(cityId)?.Name ?? cityId;

        static string PaymentLabel(Route route, ClaimOption option)
        {
            if (option.Color == CardColor.Locomotive) return $"Только локомотивы ×{option.Locomotives}";
            int colorCards = route.Length - option.Locomotives;
            string main = $"{Palette.CardTitle(option.Color)} ×{colorCards}";
            return option.Locomotives == 0 ? main : $"{main} + локомотивы ×{option.Locomotives}";
        }

        void EnsureStyles()
        {
            if (_text != null) return;

            _text = new GUIStyle(GUI.skin.label) { fontSize = 15, wordWrap = true };
            _text.normal.textColor = Palette.PanelText;

            _textSmall = new GUIStyle(_text) { fontSize = 12 };
            _textDim = new GUIStyle(_textSmall);
            _textDim.normal.textColor = Palette.PanelTextDim;
            _rightSmall = new GUIStyle(_textSmall) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold };

            _title = new GUIStyle(_text) { fontSize = 18, fontStyle = FontStyle.Bold, wordWrap = false };
            _big = new GUIStyle(_title) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            _centered = new GUIStyle(_text) { alignment = TextAnchor.UpperCenter };

            _cityLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _cityLabel.normal.textColor = Palette.MapText;
            _mapTitle = new GUIStyle(_cityLabel) { fontSize = 20, alignment = TextAnchor.MiddleLeft };

            _cardLabel = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.UpperCenter,
                clipping = TextClipping.Clip,
                wordWrap = false,
                padding = new RectOffset(0, 0, 0, 0)
            };
            _cardCount = new GUIStyle(_cardLabel) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };

            _button = new GUIStyle(GUI.skin.button) { fontSize = 15 };
        }
    }
}
