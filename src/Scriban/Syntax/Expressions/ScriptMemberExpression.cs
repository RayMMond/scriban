// Copyright (c) Alexandre Mutel. All rights reserved.
// Licensed under the BSD-Clause 2 license.
// See license.txt file in the project root for full license information.

using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Scriban.Helpers;

namespace Scriban.Syntax
{
    [ScriptSyntax("member expression", "<expression>.<variable_name>")]
    public partial class ScriptMemberExpression : ScriptExpression, IScriptVariablePath
    {
        private ScriptExpression _target;
        private ScriptToken _dotToken;
        private ScriptVariable _member;

        public ScriptMemberExpression()
        {
            DotToken = ScriptToken.Dot();
        }

        public ScriptExpression Target
        {
            get => _target;
            set => ParentToThis(ref _target, value);
        }

        public ScriptToken DotToken
        {
            get => _dotToken;
            set => ParentToThis(ref _dotToken, value);
        }

        public ScriptVariable Member
        {
            get => _member;
            set => ParentToThis(ref _member, value);
        }

        public override object Evaluate(TemplateContext context)
        {
            return context.GetValue(this);
        }

        public override void PrintTo(ScriptPrinter printer)
        {
            printer.Write(Target);
            printer.Write(DotToken);
            printer.Write(Member);
        }

        public override bool CanHaveLeadingTrivia()
        {
            return false;
        }

        public virtual object GetValue(TemplateContext context)
        {
            var targetObject = GetTargetObject(context, false);
            // In case TemplateContext.EnableRelaxedMemberAccess
            if (targetObject == null)
            {
                return null;
            }

            var accessor = context.GetMemberAccessor(targetObject);

            var memberName = this.Member.Name;

            object value;
            if (!accessor.TryGetValue(context, Span, targetObject, memberName, out value))
            {
                context.TryGetMember?.Invoke(context, Span, targetObject, memberName, out value);
            }
            return value;
        }

        public virtual void SetValue(TemplateContext context, object valueToSet)
        {
            var targetObject = GetTargetObject(context, true);
            var accessor = context.GetMemberAccessor(targetObject);

            var memberName = this.Member.Name;

            if (!accessor.TrySetValue(context, this.Span, targetObject, memberName, valueToSet))
            {
                throw new ScriptRuntimeException(this.Member.Span, $"Cannot set a value for the readonly member: {this}"); // unit test: 132-member-accessor-error3.txt
            }
        }

        public virtual string GetFirstPath()
        {
            return (Target as IScriptVariablePath)?.GetFirstPath();
        }

        private object GetTargetObject(TemplateContext context, bool isSet)
        {
            var targetObject = context.GetValue(Target);

            if (targetObject == null)
            {
                if (isSet || !context.EnableRelaxedMemberAccess)
                {
                    throw new ScriptRuntimeException(this.Span, $"Object `{this.Target}` is null. Cannot access member: {this}"); // unit test: 131-member-accessor-error1.txt
                }
            }
            else if (targetObject is string || targetObject.GetType().IsPrimitiveOrDecimal())
            {
                if (isSet || !context.EnableRelaxedMemberAccess)
                {
                    throw new ScriptRuntimeException(this.Span, $"Cannot get or set a member on the primitive `{targetObject}/{targetObject.GetType()}` when accessing member: {this}"); // unit test: 132-member-accessor-error2.txt
                }

                // If this is relaxed, set the target object to null
                if (context.EnableRelaxedMemberAccess)
                {
                    targetObject = null;
                }
            }

            return targetObject;
        }
    }
}