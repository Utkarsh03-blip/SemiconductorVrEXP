using System;
using NUnit.Framework;

namespace PNJunctionLab.Tests
{
    public class SemiconductorModelTests
    {
        [Test]
        public void Intrinsic_AtZeroKelvin_HasZeroFreeCarriers()
        {
            CarrierConcentrations si0 = SemiconductorModel.ComputeIntrinsic(SemiconductorMaterial.Silicon, 0.0);
            CarrierConcentrations ge0 = SemiconductorModel.ComputeIntrinsic(SemiconductorMaterial.Germanium, 0.0);

            Assert.AreEqual(0.0, si0.ElectronsCm3);
            Assert.AreEqual(0.0, si0.HolesCm3);
            Assert.AreEqual(0.0, si0.ConductivitySPerCm);
            Assert.AreEqual(0.0, ge0.ElectronsCm3);
            Assert.AreEqual(0.0, ge0.HolesCm3);
        }

        [Test]
        public void Intrinsic_At300K_MatchesAuthoritativeValuesAndEquality()
        {
            CarrierConcentrations si = SemiconductorModel.ComputeIntrinsic(SemiconductorMaterial.Silicon, 300.0);
            CarrierConcentrations ge = SemiconductorModel.ComputeIntrinsic(SemiconductorMaterial.Germanium, 300.0);

            Assert.AreEqual(1.5e10, si.IntrinsicNiCm3, 1.0);
            Assert.AreEqual(si.IntrinsicNiCm3, si.ElectronsCm3, 1.0);
            Assert.AreEqual(si.IntrinsicNiCm3, si.HolesCm3, 1.0);

            Assert.AreEqual(2.4e13, ge.IntrinsicNiCm3, 1.0);
            Assert.AreEqual(ge.IntrinsicNiCm3, ge.ElectronsCm3, 1.0);
            Assert.AreEqual(ge.IntrinsicNiCm3, ge.HolesCm3, 1.0);

            // Conductivity rises with temperature.
            CarrierConcentrations siHot = SemiconductorModel.ComputeIntrinsic(SemiconductorMaterial.Silicon, 450.0);
            Assert.Greater(siHot.IntrinsicNiCm3, si.IntrinsicNiCm3);
            Assert.Greater(siHot.ConductivitySPerCm, si.ConductivitySPerCm);
        }

        [Test]
        public void Extrinsic_NTypeAndPType_SatisfyMassActionAndChargeNeutrality()
        {
            double nd = 1e16;
            CarrierConcentrations nType = SemiconductorModel.ComputeNType(SemiconductorMaterial.Silicon, nd, 300.0);

            Assert.That(nType.ElectronsCm3, Is.EqualTo(nd).Within(1e10));
            Assert.AreEqual(nType.IntrinsicNiCm3 * nType.IntrinsicNiCm3, nType.ElectronsCm3 * nType.HolesCm3, 1e14);
            Assert.Greater(nType.ElectronsCm3, nType.HolesCm3);
            Assert.Greater(nType.FermiShiftFromMidgapEv, 0.0);

            double na = 2e17;
            CarrierConcentrations pType = SemiconductorModel.ComputePType(SemiconductorMaterial.Silicon, na, 300.0);

            Assert.That(pType.HolesCm3, Is.EqualTo(na).Within(1e10));
            Assert.AreEqual(pType.IntrinsicNiCm3 * pType.IntrinsicNiCm3, pType.ElectronsCm3 * pType.HolesCm3, 1e14);
            Assert.Greater(pType.HolesCm3, pType.ElectronsCm3);
            Assert.Less(pType.FermiShiftFromMidgapEv, 0.0);
        }

        [Test]
        public void ThermalVoltage_At300K_IsApprox25_9mV()
        {
            double vt = SemiconductorModel.ThermalVoltageV(300.0);
            Assert.That(vt, Is.EqualTo(0.02585).Within(0.0002));
        }

        [Test]
        public void BuiltInPotential_And_DepletionWidth_MatchReferenceSiliconExample()
        {
            // Authoritative verification example from Docs/SCIENCE.md:
            // Si, 300 K, NA = ND = 1e16 cm^-3 -> V0 ~= 0.69 V, W ~= 0.4 um
            JunctionElectrostatics eq = SemiconductorModel.ComputeJunctionElectrostatics(
                SemiconductorMaterial.Silicon, 1e16, 1e16, 0.0, 300.0);

            Assert.That(eq.BuiltInPotentialV, Is.EqualTo(0.69).Within(0.02));
            Assert.That(eq.DepletionWidthMicrometers, Is.EqualTo(0.42).Within(0.05));
            Assert.AreEqual(eq.NWidthCm, eq.PWidthCm, 1e-12);
            Assert.AreEqual(eq.DepletionWidthCm, eq.NWidthCm + eq.PWidthCm, 1e-12);

            // Germanium typical V0 ~= 0.3 V
            double geV0 = SemiconductorModel.BuiltInPotentialV(SemiconductorMaterial.Germanium, 1e16, 1e16, 300.0);
            Assert.That(geV0, Is.EqualTo(0.32).Within(0.06));
        }

        [Test]
        public void DepletionRegion_ExtendsFurtherIntoLighterDopedSide_AndRespondsToBias()
        {
            // NA = 1e15 (lighter P side), ND = 1e17 (heavier N side) -> xp >> xn
            JunctionElectrostatics asym = SemiconductorModel.ComputeJunctionElectrostatics(
                SemiconductorMaterial.Silicon, 1e15, 1e17, 0.0, 300.0);

            Assert.Greater(asym.PWidthCm, asym.NWidthCm * 50.0);
            Assert.That(asym.PWidthCm / asym.NWidthCm, Is.EqualTo(100.0).Within(1e-6));

            // Forward bias narrows depletion width; reverse bias widens it.
            JunctionElectrostatics fwd = SemiconductorModel.ComputeJunctionElectrostatics(
                SemiconductorMaterial.Silicon, 1e16, 1e16, 0.4, 300.0);
            JunctionElectrostatics eq = SemiconductorModel.ComputeJunctionElectrostatics(
                SemiconductorMaterial.Silicon, 1e16, 1e16, 0.0, 300.0);
            JunctionElectrostatics rev = SemiconductorModel.ComputeJunctionElectrostatics(
                SemiconductorMaterial.Silicon, 1e16, 1e16, -5.0, 300.0);

            Assert.Less(fwd.DepletionWidthCm, eq.DepletionWidthCm);
            Assert.Greater(rev.DepletionWidthCm, eq.DepletionWidthCm);
            Assert.Less(fwd.EffectiveBarrierV, eq.EffectiveBarrierV);
            Assert.Greater(rev.EffectiveBarrierV, eq.EffectiveBarrierV);
        }

        [Test]
        public void ShockleyEquation_ProducesExpectedForwardReverseAndTemperatureShift()
        {
            double is300 = SemiconductorModel.DiodeSaturationCurrentA(DiodeType.Silicon, 300.0);
            double zeroCurrent = SemiconductorModel.ShockleyCurrentA(0.0, is300, 1.5, 300.0);
            Assert.AreEqual(0.0, zeroCurrent, 1e-18);

            // Reverse bias saturates near -Is
            double revCurrent = SemiconductorModel.ShockleyCurrentA(-1.0, is300, 1.5, 300.0);
            Assert.That(revCurrent, Is.EqualTo(-is300).Within(1e-15));

            // Forward bias grows exponentially and exceeds knee around 0.7 V
            double subKnee = SemiconductorModel.ShockleyCurrentA(0.3, is300, 1.5, 300.0);
            double aboveKnee = SemiconductorModel.ShockleyCurrentA(0.75, is300, 1.5, 300.0);
            Assert.Greater(aboveKnee, subKnee * 1000.0);

            // Saturation current rises with temperature; forward voltage at fixed current drops (~ -2 mV/C)
            double is350 = SemiconductorModel.DiodeSaturationCurrentA(DiodeType.Silicon, 350.0);
            Assert.Greater(is350, is300);

            double vf300 = SemiconductorModel.ForwardVoltageAtCurrentV(DiodeType.Silicon, 0.01, 300.0);
            double vf350 = SemiconductorModel.ForwardVoltageAtCurrentV(DiodeType.Silicon, 0.01, 350.0);
            double deltaMvPerC = ((vf350 - vf300) * 1000.0) / 50.0;
            Assert.Less(deltaMvPerC, -1.0);
            Assert.Greater(deltaMvPerC, -3.5);
        }

        [Test]
        public void BreakdownAndSeriesCircuit_BehaveCorrectly()
        {
            DiodeParameters zener = SemiconductorModel.GetDiodeParameters(DiodeType.Zener);
            double preBreakdown = SemiconductorModel.DiodeCurrentWithBreakdownA(DiodeType.Zener, -3.0, 300.0);
            double postBreakdown = SemiconductorModel.DiodeCurrentWithBreakdownA(DiodeType.Zener, -6.5, 300.0);

            Assert.AreEqual(BreakdownMechanism.ZenerTunneling, zener.BreakdownKind);
            Assert.Less(Math.Abs(preBreakdown), 1e-6);
            Assert.Greater(Math.Abs(postBreakdown), 0.1);

            SemiconductorModel.SolveSeriesDiodeCircuit(DiodeType.Silicon, 5.0, 220.0, 300.0, out double vd, out double i);
            Assert.That(vd, Is.InRange(0.55, 0.90));
            Assert.That(i, Is.EqualTo((5.0 - vd) / 220.0).Within(1e-6));
        }

        [Test]
        public void JunctionProfile_MatchesSharpGradientAtT0_AndPoissonEquilibriumAtT1()
        {
            const double na = 1e15;
            const double nd = 1e17;
            JunctionElectrostatics eq = SemiconductorModel.ComputeJunctionElectrostatics(
                SemiconductorMaterial.Silicon, na, nd, 0.0, 300.0);
            double halfSpanCm = Math.Max(eq.PWidthCm, eq.NWidthCm) * 1.35;

            var t0Buffer = new JunctionSpatialSample[65];
            SemiconductorModel.SampleJunctionProfile(
                SemiconductorMaterial.Silicon, na, nd, 0.0, 300.0, 0.0, halfSpanCm, t0Buffer);

            // At t=0: zero space charge, zero E-field, zero potential barrier, sharp doping step across x=0.
            Assert.AreEqual(0.0, t0Buffer[32].ChargeDensityCPerCm3, 1e-12);
            Assert.AreEqual(0.0, t0Buffer[32].ElectricFieldVPerCm, 1e-12);
            Assert.AreEqual(0.0, t0Buffer[64].PotentialV, 1e-12);
            Assert.That(t0Buffer[31].HoleConcentrationCm3, Is.EqualTo(na).Within(1e10));
            Assert.That(t0Buffer[33].ElectronConcentrationCm3, Is.EqualTo(nd).Within(1e12));

            var eqBuffer = new JunctionSpatialSample[65];
            SemiconductorModel.SampleJunctionProfile(
                SemiconductorMaterial.Silicon, na, nd, 0.0, 300.0, 1.0, halfSpanCm, eqBuffer);

            // At t=1 equilibrium: potential rises by V0, electric field peaks negative at x=0 (-|Emax|),
            // quasi-Fermi levels converge flat to 0, and Ec bends downward by V0 across the junction.
            Assert.AreEqual(0.0, eqBuffer[0].PotentialV, 1e-9);
            Assert.That(eqBuffer[64].PotentialV, Is.EqualTo(eq.BuiltInPotentialV).Within(1e-4));
            Assert.That(eqBuffer[32].ElectricFieldVPerCm, Is.EqualTo(-eq.PeakElectricFieldVPerCm).Within(50.0));
            Assert.AreEqual(0.0, eqBuffer[0].QuasiFermiHoleEv, 1e-9);
            Assert.AreEqual(0.0, eqBuffer[64].QuasiFermiElectronEv, 1e-9);
            Assert.That(eqBuffer[0].EcEv - eqBuffer[64].EcEv, Is.EqualTo(eq.BuiltInPotentialV).Within(1e-4));
        }
    }
}
