using System.Collections.Generic;
using System.Text.Json;
using MindmapApp.Models;

namespace MindmapApp.Services
{
    public class UndoRedoService
    {
        // Using List to allow removing from the bottom (oldest history) easily
        private readonly List<MindmapDocument> _undoList = new List<MindmapDocument>();
        private readonly List<MindmapDocument> _redoList = new List<MindmapDocument>();
        private readonly int _maxHistory = 20;

        public bool CanUndo => _undoList.Count > 0;
        public bool CanRedo => _redoList.Count > 0;

        public void Reset()
        {
            _undoList.Clear();
            _redoList.Clear();
        }

        public void RecordState(MindmapDocument currentState)
        {
            if (currentState == null) return;
            
            // Fix: Prevent recording duplicate states (e.g. clicking without moving)
            if (_undoList.Count > 0)
            {
                var lastState = _undoList[_undoList.Count - 1];
                if (AreStatesEqual(lastState, currentState))
                {
                    return;
                }
            }

            _undoList.Add(currentState);
            
            // Limit history size
            if (_undoList.Count > _maxHistory)
            {
                _undoList.RemoveAt(0);
            }
            
            _redoList.Clear();
        }

        public MindmapDocument? Undo(MindmapDocument currentState)
        {
            if (_undoList.Count == 0) return null;

            // Optional: If current state is different from top snapshot (due to unsaved changes not yet recorded),
            // we should technically save it to Redo, but typical Undo behavior expects to jump back.
            // However, to avoid "requiring double undo", we must ensure we are definitely going back.
            
            var previousState = _undoList[_undoList.Count - 1];
            
            // Edge case: If current state IS the same as previous (e.g. we just recorded it),
            // and we undo, we might just be reloading the same thing.
            // Ideally MainViewModel handles when to record. 
            // BUT, if we have [A, B] and current is B. Undo pops B, restores B. No change.
            // So if top of stack == current, we should pop it AND the one before it?
            
            if (AreStatesEqual(previousState, currentState))
            {
                _undoList.RemoveAt(_undoList.Count - 1);
                _redoList.Add(currentState); // Save B
                
                if (_undoList.Count == 0) return null;
                
                previousState = _undoList[_undoList.Count - 1];
            }

            _undoList.RemoveAt(_undoList.Count - 1);
            _redoList.Add(currentState);
            
            return previousState;
        }

        public MindmapDocument? Redo(MindmapDocument currentState)
        {
            if (_redoList.Count == 0) return null;

            var nextState = _redoList[_redoList.Count - 1];
            _redoList.RemoveAt(_redoList.Count - 1);

            _undoList.Add(currentState);

            return nextState;
        }

        private bool AreStatesEqual(MindmapDocument a, MindmapDocument b)
        {
            if (a == null || b == null) return false;
            // Simple robust comparison using JSON
            string jsonA = JsonSerializer.Serialize(a);
            string jsonB = JsonSerializer.Serialize(b);
            return jsonA == jsonB;
        }
    }
}
