using System;
using System.Collections.Generic;

namespace Utils.FSM
{
    public class StateMachine
    {

        public State CurrentState { get; private set; }

        public StateMachine(State startState)
        {
            CurrentState = startState;
        }

        public void Update(object owner)
        {
            foreach (var transition in CurrentState.Transitions)
            {
                if (transition.Condition(owner))
                {
                    CurrentState.OnExit(owner);
                    CurrentState = transition.NextState;
                    CurrentState.OnEnter(owner);
                    break;
                }
            }

            CurrentState.Update(owner);
        }

    }

    public class State
    {
        public List<Transition> Transitions { get; private set; } = new List<Transition>();

        public State()
        {
        }

        public virtual void OnEnter(object owner) { }
        public virtual void Update(object owner) { }
        public virtual void OnExit(object owner) { }

        public void AddTransition(State nextState, Func<object, bool> condition)
        {
            Transitions.Add(new Transition(nextState, condition));
        }
    }

    public class Transition
    {
        public State NextState { get; private set; }
        public Func<object,bool> Condition { get; private set; }

        public Transition(State nextState, Func<object, bool> condition)
        {
            NextState = nextState;
            Condition = condition;
        }
    }
}