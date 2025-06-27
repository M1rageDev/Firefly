using UnityEngine;

namespace FireflyAPI
{
    public interface IFireflyModule
    {
        bool OverridePhysics { get; set; }

        string OverridenBy { get; set; }
		Vector3 OverrideEntryDirection { get; set; }
		float OverrideEffectStrength { get; set; }
		float OverrideEffectState { get; set; }
		float OverrideAngleOfAttack { get; set; }
		BodyConfig OverrideBodyConfig { get; set; }

		void ResetOverride();
		void ReloadCommandBuffer();
		void CreateVesselFx();
	}
}
