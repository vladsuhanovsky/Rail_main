using System;
using System.Collections.Generic;

namespace RailGame.Core
{
    internal static class ShuffleUtil
    {
        /// <summary>Тасование Фишера — Йейтса.</summary>
        public static void InPlace<T>(IList<T> list, Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                T tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }
    }

    /// <summary>Колода карт вагонов со сбросом. Когда колода кончается, сброс тасуется и становится колодой.</summary>
    public sealed class CardDeck
    {
        readonly List<CardColor> _drawPile = new List<CardColor>();
        readonly List<CardColor> _discardPile = new List<CardColor>();
        readonly Random _rng;

        public int DrawPileCount => _drawPile.Count;
        public int DiscardPileCount => _discardPile.Count;
        /// <summary>Сколько карт ещё можно взять с учётом сброса.</summary>
        public int AvailableCount => _drawPile.Count + _discardPile.Count;

        public CardDeck(IEnumerable<CardColor> cards, Random rng)
        {
            _rng = rng;
            _drawPile.AddRange(cards);
            ShuffleUtil.InPlace(_drawPile, _rng);
        }

        public bool TryDraw(out CardColor card)
        {
            if (_drawPile.Count == 0 && _discardPile.Count > 0)
            {
                _drawPile.AddRange(_discardPile);
                _discardPile.Clear();
                ShuffleUtil.InPlace(_drawPile, _rng);
            }

            if (_drawPile.Count == 0)
            {
                card = default(CardColor);
                return false;
            }

            int last = _drawPile.Count - 1;
            card = _drawPile[last];
            _drawPile.RemoveAt(last);
            return true;
        }

        public void Discard(CardColor card) => _discardPile.Add(card);
    }

    /// <summary>Колода заданий. Возвращённые задания уходят под низ.</summary>
    public sealed class TicketDeck
    {
        readonly List<Ticket> _cards; // индекс 0 — верх колоды

        public int Count => _cards.Count;

        public TicketDeck(IEnumerable<Ticket> tickets, Random rng)
        {
            _cards = new List<Ticket>(tickets);
            ShuffleUtil.InPlace(_cards, rng);
        }

        public List<Ticket> Draw(int count)
        {
            var result = new List<Ticket>();
            while (result.Count < count && _cards.Count > 0)
            {
                result.Add(_cards[0]);
                _cards.RemoveAt(0);
            }
            return result;
        }

        public void ReturnToBottom(IEnumerable<Ticket> tickets) => _cards.AddRange(tickets);
    }
}
