using System;
using System.Reflection;
using XRL;
using XRL.Core;
using XRL.World.Skills;
using GameObject = XRL.World.GameObject;
using UnityEngine;

namespace Egocarib.Code
{
    public class NalathniAppraiseExtender
    {
        private static bool _bInitialized = false;
        private static bool _bAppraiseSkillExists = false;
        private static bool _bExceptionLogged = false;
        private bool? _bCanAppraise = null;
        private bool? _bPreventPlayerAppraisal = null;
        Type NalathniAppraise;
        object NalathniAppraiseInstance;
        MethodInfo NalathniAppraiseMethod;

        public bool PreventPlayerAppraisal //should the player be prevented from appraising (due to a lack of NalathniAppraise skill)?
        {
            get
            {
                if (this._bPreventPlayerAppraisal == null)
                {
                    GameObject player = XRLCore.Core.Game?.Player?.Body;
                    this._bPreventPlayerAppraisal = (NalathniAppraiseExtender._bAppraiseSkillExists && player != null && !player.HasSkill("NalathniAppraise"));
                }
                return (bool)this._bPreventPlayerAppraisal;
            }
        }

        public bool PlayerCanAppraise //does the player have the skill to appraise, and were we also able to load the type/method successfully?
        {
            get
            {
                if (this._bCanAppraise == null)
                {
                    GameObject player = XRLCore.Core.Game?.Player?.Body;
                    this._bCanAppraise = (NalathniAppraiseExtender._bAppraiseSkillExists && player != null && player.HasSkill("NalathniAppraise"));
                    if (this._bCanAppraise == true)
                    {
                        try
                        {
                            this.NalathniAppraise = ModManager.ResolveType("XRL.World.Parts.Skill.NalathniAppraise");
                            this.NalathniAppraiseInstance = NalathniAppraise == null ? null : Activator.CreateInstance(NalathniAppraise);
                            this.NalathniAppraiseMethod = NalathniAppraise == null ? null : NalathniAppraise.GetMethod("Approximate");
                            if (this.NalathniAppraiseMethod == null)
                            {
                                throw new Exception("NalathniAppraise.GetMethod(\"Approximate\") failed to retrieve the expected MethodInfo. "
                                                   + "[NalathniAppraise Type is null? : " + (this.NalathniAppraise == null) + "]");
                            }
                        }
                        catch (Exception ex)
                        {
                            this._bCanAppraise = false; //prevent appraisal if we can't get the type & method
                            if (!NalathniAppraiseExtender._bExceptionLogged)
                            {
                                NalathniAppraiseExtender._bExceptionLogged = true;
                                Debug.Log("QudUX Mod: Unable to invoke NalathniAppraise.Approximate(). Falling back to default valuation method.\nException: " + ex.ToString());
                            }
                        }
                    }
                }
                return (bool)this._bCanAppraise;
            }
        }

        public NalathniAppraiseExtender()
        {
            if (NalathniAppraiseExtender._bInitialized != true)
            {
                NalathniAppraiseExtender._bInitialized = true;
                if (SkillFactory.Factory.SkillByClass.ContainsKey("Customs"))
                {
                    if (SkillFactory.Factory.SkillByClass["Customs"].Powers.ContainsKey("Appraisal"))
                    {
                        if (SkillFactory.Factory.SkillByClass["Customs"].Powers["Appraisal"].Class == "NalathniAppraise")
                        {
                            NalathniAppraiseExtender._bAppraiseSkillExists = true;
                            SkillFactory.Factory.SkillByClass["Customs"].Powers["Appraisal"].Description += "\nPress ALT on the Inventory screen to appraise items in bulk.";
                            Debug.Log("QudUX Mod: Recognized NalathniDragon's Appraisal Skill mod.\n    Updating Appraisal skill description...\n    Restricting ALT keybind in inventory to Appraisal skill...");
                        }
                    }
                }
            }
        }

        public int Approximate(double rawValue)
        {
            int approximatedValue = -999;
            if (this.PlayerCanAppraise)
            {
                try
                {
                    approximatedValue = (int)this.NalathniAppraiseMethod.Invoke(this.NalathniAppraiseInstance, new object[] { rawValue });
                    approximatedValue = approximatedValue >= 0 ? approximatedValue : -999;
                }
                catch (Exception ex)
                {
                    if (!NalathniAppraiseExtender._bExceptionLogged)
                    {
                        NalathniAppraiseExtender._bExceptionLogged = true;
                        Debug.Log("QudUX Mod: Error encountered while invoking NalathniAppraise.Approximate(). Falling back to default valuation method.\nException: " + ex.ToString());
                    }
                    approximatedValue = -999;
                }
            }
            return approximatedValue;
        }
    }
}