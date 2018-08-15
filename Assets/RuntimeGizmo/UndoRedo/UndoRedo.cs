using System;
using System.Collections.Generic;

namespace CommandUndoRedo
{
	public class UndoRedo
	{
		public int maxUndoStored = int.MaxValue;

		Stack<ICommand> undoCommands = new Stack<ICommand>();
		Stack<ICommand> redoCommands = new Stack<ICommand>();

		public UndoRedo() {}
		public UndoRedo(int maxUndoStored)
		{
			this.maxUndoStored = maxUndoStored;
		}

		public void Clear()
		{
			undoCommands.Clear();
			redoCommands.Clear();
		}

		public void Undo()
		{
			if(undoCommands.Count > 0)
			{
				ICommand command = undoCommands.Pop();
				command.UnExecute();
				redoCommands.Push(command);
			}
		}

		public void Redo()
		{
			if(redoCommands.Count > 0)
			{
				ICommand command = redoCommands.Pop();
				command.Execute();
				undoCommands.Push(command);
			}
		}

		public void Insert(ICommand command)
		{
			if(undoCommands.Count > 0 && undoCommands.Count >= maxUndoStored)
			{
				undoCommands.Pop();
			}

			if(maxUndoStored <= 0) return;

			undoCommands.Push(command);
			redoCommands.Clear();
		}

		public void Execute(ICommand command)
		{
			command.Execute();
			Insert(command);
		}
	}
}
