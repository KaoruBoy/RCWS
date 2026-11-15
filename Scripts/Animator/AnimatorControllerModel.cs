#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.Core;

namespace RCWS
{
    public  class AnimatorControllerModel
    {
        public readonly List<AnimatorParameterModel> Parameters = new List<AnimatorParameterModel>();
        public readonly List<AnimatorLayerModel> Layers = new List<AnimatorLayerModel>();

        /// <summary>Creates a model of the controller's parameters, layers, states, and transitions.</summary>
        public static AnimatorControllerModel FromAnimatorController(AnimatorController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            var model = new AnimatorControllerModel();
            var states = new Dictionary<AnimatorState, AnimatorStateModel>();
            var stateMachines = new Dictionary<AnimatorStateMachine, AnimatorStateMachineModel>();
            var parameters = new Dictionary<string, AnimatorParameterModel>();

            foreach (var parameter in controller.parameters)
            {
                var parameterModel = new AnimatorParameterModel(parameter);
                model.Parameters.Add(parameterModel);
                parameters.Add(parameter.name, parameterModel);
            }

            foreach (var layer in controller.layers)
            {
                var layerModel = new AnimatorLayerModel
                {
                    Name = layer.name,
                    AvatarMask = layer.avatarMask,
                    BlendingMode = layer.blendingMode,
                    DefaultWeight = layer.defaultWeight,
                    IKPass = layer.iKPass,
                    SyncedLayerIndex = layer.syncedLayerIndex,
                    SyncedLayerAffectsTiming = layer.syncedLayerAffectsTiming,
                    StateMachine = CaptureStructure(layer.stateMachine, states, stateMachines, parameters)
                };
                model.Layers.Add(layerModel);
            }

            foreach (var layer in controller.layers)
                CaptureTransitions(layer.stateMachine, states, stateMachines, parameters);

            return model;
        }

        /// <summary>
        /// Replaces same-named controller parameters and appends the model's layers.
        /// Synced layer indexes are adjusted to account for the appended layers.
        /// </summary>
        public void AddLayersAndParametersTo(AnimatorController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (Layers.Any(layer => layer == null || layer.StateMachine == null))
                throw new InvalidOperationException("Every animator layer must have a state machine.");

            var parameterNames = CreateParameterNameMap();
            CopyParametersTo(controller);

            var firstNewLayer = controller.layers.Length;
            foreach (var layerModel in Layers)
                controller.AddLayer(layerModel.Name);

            var layers = controller.layers;
            for (var i = 0; i < Layers.Count; i++)
            {
                var model = Layers[i];
                var layerIndex = firstNewLayer + i;
                var layer = layers[layerIndex];
                layer.name = model.Name;
                layer.avatarMask = model.AvatarMask;
                layer.blendingMode = model.BlendingMode;
                layer.defaultWeight = model.DefaultWeight;
                layer.iKPass = model.IKPass;
                layer.syncedLayerIndex = model.SyncedLayerIndex < 0
                    ? -1
                    : firstNewLayer + model.SyncedLayerIndex;
                layer.syncedLayerAffectsTiming = model.SyncedLayerAffectsTiming;
                layers[layerIndex] = layer;
            }

            controller.layers = layers;

            for (var i = 0; i < Layers.Count; i++)
            {
                var layerIndex = firstNewLayer + i;
                var machine = controller.layers[layerIndex].stateMachine;
                var states = new Dictionary<AnimatorStateModel, AnimatorState>();
                var stateMachines = new Dictionary<AnimatorStateMachineModel, AnimatorStateMachine>();
                CreateStateMachineStructure(
                    Layers[i].StateMachine, machine, controller, states, stateMachines, parameterNames);
                CreateTransitions(Layers[i].StateMachine, states, stateMachines, parameterNames);
            }
        }

        public void CopyParametersTo(AnimatorController controller)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));

            var parameters = controller.parameters.ToList();
            foreach (var model in Parameters)
            {
                var parameter = model.ToUnityParameter();
                var existingIndex = parameters.FindIndex(p => p.name == parameter.name);
                if (existingIndex < 0)
                    parameters.Add(parameter);
                else
                    parameters[existingIndex] = parameter;
            }

            controller.parameters = parameters.ToArray();
        }

        static AnimatorStateMachineModel CaptureStructure(
            AnimatorStateMachine source,
            IDictionary<AnimatorState, AnimatorStateModel> states,
            IDictionary<AnimatorStateMachine, AnimatorStateMachineModel> stateMachines,
            IDictionary<string, AnimatorParameterModel> parameters)
        {
            if (source == null) return null;

            var model = new AnimatorStateMachineModel
            {
                Name = source.name,
                AnyStatePosition = source.anyStatePosition,
                EntryPosition = source.entryPosition,
                ExitPosition = source.exitPosition,
                ParentStateMachinePosition = source.parentStateMachinePosition,
                Behaviours = CloneBehaviours(source.behaviours)
            };
            stateMachines.Add(source, model);

            foreach (var child in source.states)
            {
                var state = child.state;
                var stateModel = new AnimatorStateModel
                {
                    Name = state.name,
                    Tag = state.tag,
                    Speed = state.speed,
                    CycleOffset = state.cycleOffset,
                    Mirror = state.mirror,
                    IKOnFeet = state.iKOnFeet,
                    WriteDefaultValues = state.writeDefaultValues,
                    SpeedParameterActive = state.speedParameterActive,
                    SpeedParameter = FindParameter(state.speedParameter, parameters),
                    CycleOffsetParameterActive = state.cycleOffsetParameterActive,
                    CycleOffsetParameter = FindParameter(state.cycleOffsetParameter, parameters),
                    MirrorParameterActive = state.mirrorParameterActive,
                    MirrorParameter = FindParameter(state.mirrorParameter, parameters),
                    TimeParameterActive = state.timeParameterActive,
                    TimeParameter = FindParameter(state.timeParameter, parameters),
                    Position = child.position,
                    Motion = AnimatorMotionModel.FromMotion(state.motion, parameters),
                    Behaviours = CloneBehaviours(state.behaviours)
                };
                model.States.Add(stateModel);
                states.Add(state, stateModel);
                if (source.defaultState == state)
                    model.DefaultState = stateModel;
            }

            foreach (var child in source.stateMachines)
            {
                var childModel = CaptureStructure(child.stateMachine, states, stateMachines, parameters);
                childModel.Position = child.position;
                model.StateMachines.Add(childModel);
            }

            return model;
        }

        static void CaptureTransitions(
            AnimatorStateMachine source,
            IDictionary<AnimatorState, AnimatorStateModel> states,
            IDictionary<AnimatorStateMachine, AnimatorStateMachineModel> stateMachines,
            IDictionary<string, AnimatorParameterModel> parameters)
        {
            if (source == null) return;

            var machineModel = stateMachines[source];
            foreach (var state in source.states)
            {
                var stateModel = states[state.state];
                foreach (var transition in state.state.transitions)
                    stateModel.Transitions.Add(
                        AnimatorTransitionModel.FromTransition(transition, states, stateMachines, parameters));
            }

            foreach (var transition in source.anyStateTransitions)
                machineModel.AnyStateTransitions.Add(
                    AnimatorTransitionModel.FromTransition(transition, states, stateMachines, parameters));

            foreach (var transition in source.entryTransitions)
                machineModel.EntryTransitions.Add(
                    AnimatorTransitionModel.FromTransition(transition, states, stateMachines, parameters));

            foreach (var child in source.stateMachines)
            {
                foreach (var transition in source.GetStateMachineTransitions(child.stateMachine))
                {
                    machineModel.StateMachineTransitions.Add(new AnimatorStateMachineTransitionModel
                    {
                        SourceStateMachine = stateMachines[child.stateMachine],
                        Transition = AnimatorTransitionModel.FromTransition(
                            transition, states, stateMachines, parameters)
                    });
                }
                CaptureTransitions(child.stateMachine, states, stateMachines, parameters);
            }
        }

        static void CreateStateMachineStructure(
            AnimatorStateMachineModel model,
            AnimatorStateMachine destination,
            AnimatorController controller,
            IDictionary<AnimatorStateModel, AnimatorState> states,
            IDictionary<AnimatorStateMachineModel, AnimatorStateMachine> machines,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            if (model == null || destination == null) return;

            machines.Add(model, destination);
            destination.name = model.Name;
            destination.anyStatePosition = model.AnyStatePosition;
            destination.entryPosition = model.EntryPosition;
            destination.exitPosition = model.ExitPosition;
            destination.parentStateMachinePosition = model.ParentStateMachinePosition;
            CopyBehaviours(model.Behaviours, destination);

            foreach (var stateModel in model.States)
            {
                var state = destination.AddState(stateModel.Name, stateModel.Position);
                state.tag = stateModel.Tag;
                state.speed = stateModel.Speed;
                state.cycleOffset = stateModel.CycleOffset;
                state.mirror = stateModel.Mirror;
                state.iKOnFeet = stateModel.IKOnFeet;
                state.writeDefaultValues = stateModel.WriteDefaultValues;
                state.speedParameterActive = stateModel.SpeedParameterActive;
                state.speedParameter = GetParameterName(
                    stateModel.SpeedParameter, parameterNames, stateModel.SpeedParameterActive);
                state.cycleOffsetParameterActive = stateModel.CycleOffsetParameterActive;
                state.cycleOffsetParameter = GetParameterName(
                    stateModel.CycleOffsetParameter, parameterNames, stateModel.CycleOffsetParameterActive);
                state.mirrorParameterActive = stateModel.MirrorParameterActive;
                state.mirrorParameter = GetParameterName(
                    stateModel.MirrorParameter, parameterNames, stateModel.MirrorParameterActive);
                state.timeParameterActive = stateModel.TimeParameterActive;
                state.timeParameter = GetParameterName(
                    stateModel.TimeParameter, parameterNames, stateModel.TimeParameterActive);
                state.motion = BuildMotion(stateModel.Motion, controller, parameterNames);
                CopyBehaviours(stateModel.Behaviours, state);
                states.Add(stateModel, state);
            }

            foreach (var childModel in model.StateMachines)
            {
                var child = destination.AddStateMachine(childModel.Name, childModel.Position);
                CreateStateMachineStructure(childModel, child, controller, states, machines, parameterNames);
            }

            if (model.DefaultState != null && !states.TryGetValue(model.DefaultState, out _))
                throw new InvalidOperationException("A state machine's default state must belong to that state machine.");
            destination.defaultState = model.DefaultState == null ? null : states[model.DefaultState];
        }

        static void CreateTransitions(
            AnimatorStateMachineModel model,
            IDictionary<AnimatorStateModel, AnimatorState> states,
            IDictionary<AnimatorStateMachineModel, AnimatorStateMachine> machines,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            var destination = machines[model];
            foreach (var stateModel in model.States)
            {
                var state = states[stateModel];
                foreach (var transitionModel in stateModel.Transitions)
                    CreateStateTransition(state, transitionModel, states, machines, parameterNames);
            }

            foreach (var transitionModel in model.AnyStateTransitions)
                CreateAnyStateTransition(destination, transitionModel, states, machines, parameterNames);

            foreach (var transitionModel in model.EntryTransitions)
                CreateEntryTransition(destination, transitionModel, states, machines, parameterNames);

            foreach (var transitionModel in model.StateMachineTransitions)
            {
                if (transitionModel.SourceStateMachine == null ||
                    !machines.TryGetValue(transitionModel.SourceStateMachine, out var source))
                    continue;

                CreateStateMachineTransition(
                    destination, source, transitionModel.Transition, states, machines, parameterNames);
            }

            foreach (var childModel in model.StateMachines)
                CreateTransitions(childModel, states, machines, parameterNames);
        }

        static Motion BuildMotion(
            AnimatorMotionModel model,
            AnimatorController controller,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            if (model == null) return null;
            if (model.BlendTree == null) return model.Motion;

            var tree = new BlendTree
            {
                name = model.BlendTree.Name,
                blendType = model.BlendTree.BlendType,
                blendParameter = GetParameterName(model.BlendTree.BlendParameter, parameterNames, false),
                blendParameterY = GetParameterName(model.BlendTree.BlendParameterY, parameterNames, false),
                minThreshold = model.BlendTree.MinThreshold,
                maxThreshold = model.BlendTree.MaxThreshold,
                useAutomaticThresholds = model.BlendTree.UseAutomaticThresholds,
            };

            if (AssetDatabase.Contains(controller))
                AssetDatabase.AddObjectToAsset(tree, controller);

            tree.children = model.BlendTree.Children.Select(child => new ChildMotion
            {
                motion = BuildMotion(child.Motion, controller, parameterNames),
                threshold = child.Threshold,
                position = child.Position,
                timeScale = child.TimeScale,
                cycleOffset = child.CycleOffset,
                mirror = child.Mirror,
                directBlendParameter = GetParameterName(child.DirectBlendParameter, parameterNames, false)
            }).ToArray();

            return tree;
        }

        static void CreateStateTransition(
            AnimatorState source,
            AnimatorTransitionModel model,
            IDictionary<AnimatorStateModel, AnimatorState> states,
            IDictionary<AnimatorStateMachineModel, AnimatorStateMachine> machines,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            AnimatorStateTransition transition;
            if (model.IsExit)
                transition = source.AddExitTransition();
            else if (model.DestinationState != null && states.TryGetValue(model.DestinationState, out var state))
                transition = source.AddTransition(state);
            else if (model.DestinationStateMachine != null && machines.TryGetValue(model.DestinationStateMachine, out var machine))
                transition = source.AddTransition(machine);
            else
                throw new InvalidOperationException("A state transition must have a valid destination or be an exit transition.");

            ApplyTransition(model, transition, parameterNames);
        }

        static void CreateAnyStateTransition(
            AnimatorStateMachine source,
            AnimatorTransitionModel model,
            IDictionary<AnimatorStateModel, AnimatorState> states,
            IDictionary<AnimatorStateMachineModel, AnimatorStateMachine> machines,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            AnimatorStateTransition transition;
            if (model.DestinationState != null && states.TryGetValue(model.DestinationState, out var state))
                transition = source.AddAnyStateTransition(state);
            else if (model.DestinationStateMachine != null && machines.TryGetValue(model.DestinationStateMachine, out var machine))
                transition = source.AddAnyStateTransition(machine);
            else
                throw new InvalidOperationException("An Any State transition must have a valid destination or be an exit transition.");

            ApplyTransition(model, transition, parameterNames);
        }

        static void CreateEntryTransition(
            AnimatorStateMachine source,
            AnimatorTransitionModel model,
            IDictionary<AnimatorStateModel, AnimatorState> states,
            IDictionary<AnimatorStateMachineModel, AnimatorStateMachine> machines,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            AnimatorTransition transition;
            if (model.DestinationState != null && states.TryGetValue(model.DestinationState, out var state))
                transition = source.AddEntryTransition(state);
            else if (model.DestinationStateMachine != null && machines.TryGetValue(model.DestinationStateMachine, out var machine))
                transition = source.AddEntryTransition(machine);
            else
                throw new InvalidOperationException("An entry transition must have a valid destination.");

            ApplyTransition(model, transition, parameterNames);
        }

        static void CreateStateMachineTransition(
            AnimatorStateMachine parent,
            AnimatorStateMachine source,
            AnimatorTransitionModel model,
            IDictionary<AnimatorStateModel, AnimatorState> states,
            IDictionary<AnimatorStateMachineModel, AnimatorStateMachine> machines,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            AnimatorTransition transition;
            if (model.IsExit)
                transition = parent.AddStateMachineExitTransition(source);
            else if (model.DestinationState != null && states.TryGetValue(model.DestinationState, out var state))
                transition = parent.AddStateMachineTransition(source, state);
            else if (model.DestinationStateMachine != null && machines.TryGetValue(model.DestinationStateMachine, out var machine))
                transition = parent.AddStateMachineTransition(source, machine);
            else
                throw new InvalidOperationException("A state machine transition must have a valid destination or be an exit transition.");

            ApplyTransition(model, transition, parameterNames);
        }

        static void ApplyTransition(
            AnimatorTransitionModel model,
            AnimatorTransitionBase transition,
            IDictionary<AnimatorParameterModel, string> parameterNames)
        {
            transition.mute = model.Mute;
            transition.solo = model.Solo;
            foreach (var condition in model.Conditions)
                transition.AddCondition(
                    condition.Mode,
                    condition.Threshold,
                    GetParameterName(condition.Parameter, parameterNames, true));

            if (transition is AnimatorStateTransition stateTransition)
            {
                stateTransition.duration = model.Duration;
                stateTransition.exitTime = model.ExitTime;
                stateTransition.hasExitTime = model.HasExitTime;
                stateTransition.hasFixedDuration = model.HasFixedDuration;
                stateTransition.offset = model.Offset;
                stateTransition.interruptionSource = model.InterruptionSource;
                stateTransition.orderedInterruption = model.OrderedInterruption;
                stateTransition.canTransitionToSelf = model.CanTransitionToSelf;
            }
        }

        static void CopyBehaviours(StateMachineBehaviour[] source, AnimatorState destination)
        {
            foreach (var behaviour in source)
            {
                if (behaviour == null) continue;
                var copy = destination.AddStateMachineBehaviour(behaviour.GetType());
                EditorUtility.CopySerialized(behaviour, copy);
            }
        }

        static void CopyBehaviours(StateMachineBehaviour[] source, AnimatorStateMachine destination)
        {
            foreach (var behaviour in source)
            {
                if (behaviour == null) continue;
                var copy = destination.AddStateMachineBehaviour(behaviour.GetType());
                EditorUtility.CopySerialized(behaviour, copy);
            }
        }

        static StateMachineBehaviour[] CloneBehaviours(StateMachineBehaviour[] source)
        {
            return source
                .Select(behaviour => behaviour == null ? null : UnityEngine.Object.Instantiate(behaviour))
                .ToArray();
        }

        static IDictionary<AnimatorParameterModel, string> CreateParameterNameMap(
            IEnumerable<AnimatorParameterModel> parameters)
        {
            var result = new Dictionary<AnimatorParameterModel, string>();
            foreach (var parameter in parameters)
            {
                if (parameter == null)
                    throw new InvalidOperationException("Animator parameter entries cannot be null.");
                if (string.IsNullOrEmpty(parameter.Name))
                    throw new InvalidOperationException("Animator parameter names cannot be empty.");
                result.Add(parameter, parameter.Name);
            }
            return result;
        }

        IDictionary<AnimatorParameterModel, string> CreateParameterNameMap()
        {
            return CreateParameterNameMap(Parameters);
        }

        static string GetParameterName(
            AnimatorParameterModel parameter,
            IDictionary<AnimatorParameterModel, string> parameterNames,
            bool required)
        {
            if (parameter == null)
            {
                if (required)
                    throw new InvalidOperationException("An active animator parameter reference is missing.");
                return string.Empty;
            }

            return parameter.Name;

            if (!parameterNames.TryGetValue(parameter, out var name))
                throw new InvalidOperationException(
                    "Animator parameter reference '" + parameter.Name + "' is not in the controller model's parameter list.");
            return name;
        }

        internal static AnimatorParameterModel FindParameter(
            string name,
            IDictionary<string, AnimatorParameterModel> parameters)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            if (!parameters.TryGetValue(name, out var parameter))
            {
                if(name == "Blend")
                {
                    parameter = new AnimatorParameterModel()
                    {
                        Name = "Blend",
                        Type = AnimatorControllerParameterType.Float,
                        DefaultFloat = 0
                    };
                    parameters.Add("Blend", parameter);
                    return parameter;
                }

                throw new InvalidOperationException(
                    "Animator refers to parameter '" + name + "' which is not declared on its controller.");
            }
            return parameter;
        }
    }
}
#endif
