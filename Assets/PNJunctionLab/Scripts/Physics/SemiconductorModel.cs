using System;

namespace PNJunctionLab
{
    public enum SemiconductorMaterial
    {
        Silicon,
        Germanium
    }

    public enum DiodeType
    {
        Silicon,
        Germanium,
        LED,
        Zener
    }

    public enum BreakdownMechanism
    {
        None,
        ZenerTunneling,
        AvalancheImpactIonisation
    }

    public readonly struct MaterialProperties
    {
        public readonly SemiconductorMaterial Material;
        public readonly string Symbol;
        public readonly double BandGapEv;
        public readonly double IntrinsicConcentration300KCm3;
        public readonly double RelativePermittivity;
        public readonly double AtomicDensityCm3;
        public readonly double ElectronMobilityCm2Vs;
        public readonly double HoleMobilityCm2Vs;

        public MaterialProperties(
            SemiconductorMaterial material,
            string symbol,
            double bandGapEv,
            double intrinsicConcentration300KCm3,
            double relativePermittivity,
            double atomicDensityCm3,
            double electronMobilityCm2Vs,
            double holeMobilityCm2Vs)
        {
            Material = material;
            Symbol = symbol;
            BandGapEv = bandGapEv;
            IntrinsicConcentration300KCm3 = intrinsicConcentration300KCm3;
            RelativePermittivity = relativePermittivity;
            AtomicDensityCm3 = atomicDensityCm3;
            ElectronMobilityCm2Vs = electronMobilityCm2Vs;
            HoleMobilityCm2Vs = holeMobilityCm2Vs;
        }
    }

    public readonly struct CarrierConcentrations
    {
        public readonly double ElectronsCm3;
        public readonly double HolesCm3;
        public readonly double IntrinsicNiCm3;
        public readonly double DonorConcentrationCm3;
        public readonly double AcceptorConcentrationCm3;
        public readonly double FermiShiftFromMidgapEv;
        public readonly double ConductivitySPerCm;

        public CarrierConcentrations(
            double electronsCm3,
            double holesCm3,
            double intrinsicNiCm3,
            double donorConcentrationCm3,
            double acceptorConcentrationCm3,
            double fermiShiftFromMidgapEv,
            double conductivitySPerCm)
        {
            ElectronsCm3 = electronsCm3;
            HolesCm3 = holesCm3;
            IntrinsicNiCm3 = intrinsicNiCm3;
            DonorConcentrationCm3 = donorConcentrationCm3;
            AcceptorConcentrationCm3 = acceptorConcentrationCm3;
            FermiShiftFromMidgapEv = fermiShiftFromMidgapEv;
            ConductivitySPerCm = conductivitySPerCm;
        }
    }

    public readonly struct JunctionElectrostatics
    {
        public readonly double BuiltInPotentialV;
        public readonly double EffectiveBarrierV;
        public readonly double DepletionWidthCm;
        public readonly double NWidthCm;
        public readonly double PWidthCm;
        public readonly double PeakElectricFieldVPerCm;

        public double DepletionWidthMicrometers => DepletionWidthCm * 1e4;
        public double NWidthMicrometers => NWidthCm * 1e4;
        public double PWidthMicrometers => PWidthCm * 1e4;

        public JunctionElectrostatics(
            double builtInPotentialV,
            double effectiveBarrierV,
            double depletionWidthCm,
            double nWidthCm,
            double pWidthCm,
            double peakElectricFieldVPerCm)
        {
            BuiltInPotentialV = builtInPotentialV;
            EffectiveBarrierV = effectiveBarrierV;
            DepletionWidthCm = depletionWidthCm;
            NWidthCm = nWidthCm;
            PWidthCm = pWidthCm;
            PeakElectricFieldVPerCm = peakElectricFieldVPerCm;
        }
    }

    public readonly struct JunctionSpatialSample
    {
        public readonly double PositionCm;
        public readonly double ElectronConcentrationCm3;
        public readonly double HoleConcentrationCm3;
        public readonly double ChargeDensityCPerCm3;
        public readonly double ElectricFieldVPerCm;
        public readonly double PotentialV;
        public readonly double EcEv;
        public readonly double EvEv;
        public readonly double QuasiFermiElectronEv;
        public readonly double QuasiFermiHoleEv;

        public JunctionSpatialSample(
            double positionCm,
            double electronConcentrationCm3,
            double holeConcentrationCm3,
            double chargeDensityCPerCm3,
            double electricFieldVPerCm,
            double potentialV,
            double ecEv,
            double evEv,
            double quasiFermiElectronEv,
            double quasiFermiHoleEv)
        {
            PositionCm = positionCm;
            ElectronConcentrationCm3 = electronConcentrationCm3;
            HoleConcentrationCm3 = holeConcentrationCm3;
            ChargeDensityCPerCm3 = chargeDensityCPerCm3;
            ElectricFieldVPerCm = electricFieldVPerCm;
            PotentialV = potentialV;
            EcEv = ecEv;
            EvEv = evEv;
            QuasiFermiElectronEv = quasiFermiElectronEv;
            QuasiFermiHoleEv = quasiFermiHoleEv;
        }
    }

    public readonly struct DiodeParameters
    {
        public readonly DiodeType Type;
        public readonly string DisplayName;
        public readonly SemiconductorMaterial BaseMaterial;
        public readonly double KneeVoltageV;
        public readonly double SaturationCurrent300KA;
        public readonly double IdealityFactor;
        public readonly double BreakdownVoltageV;
        public readonly BreakdownMechanism BreakdownKind;
        public readonly double EffectiveGapEv;

        public DiodeParameters(
            DiodeType type,
            string displayName,
            SemiconductorMaterial baseMaterial,
            double kneeVoltageV,
            double saturationCurrent300KA,
            double idealityFactor,
            double breakdownVoltageV,
            BreakdownMechanism breakdownKind,
            double effectiveGapEv)
        {
            Type = type;
            DisplayName = displayName;
            BaseMaterial = baseMaterial;
            KneeVoltageV = kneeVoltageV;
            SaturationCurrent300KA = saturationCurrent300KA;
            IdealityFactor = idealityFactor;
            BreakdownVoltageV = breakdownVoltageV;
            BreakdownKind = breakdownKind;
            EffectiveGapEv = effectiveGapEv;
        }
    }

    /// <summary>
    /// Pure C# semiconductor physics model matching Docs/SCIENCE.md.
    /// Contains zero UnityEngine dependencies so formulas are strictly testable in EditMode.
    /// </summary>
    public static class SemiconductorModel
    {
        public const double ElementaryChargeQ = 1.602176634e-19;          // C
        public const double BoltzmannConstantJPerK = 1.380649e-23;        // J/K
        public const double BoltzmannConstantEvPerK = 8.617333262e-5;     // eV/K
        public const double VacuumPermittivityFPerM = 8.854187817e-12;    // F/m
        public const double VacuumPermittivityFPerCm = 8.854187817e-14;   // F/cm
        public const double ReferenceTemperatureK = 300.0;                // K
        public const double SiliconPhosphorusDonorDepthEv = 0.045;        // eV below Ec

        private static readonly MaterialProperties SiliconProps = new MaterialProperties(
            SemiconductorMaterial.Silicon,
            "Si",
            bandGapEv: 1.12,
            intrinsicConcentration300KCm3: 1.5e10,
            relativePermittivity: 11.7,
            atomicDensityCm3: 5.0e22,
            electronMobilityCm2Vs: 1350.0,
            holeMobilityCm2Vs: 480.0);

        private static readonly MaterialProperties GermaniumProps = new MaterialProperties(
            SemiconductorMaterial.Germanium,
            "Ge",
            bandGapEv: 0.67,
            intrinsicConcentration300KCm3: 2.4e13,
            relativePermittivity: 16.0,
            atomicDensityCm3: 4.42e22,
            electronMobilityCm2Vs: 3900.0,
            holeMobilityCm2Vs: 1900.0);

        private static readonly DiodeParameters SiliconDiode = new DiodeParameters(
            DiodeType.Silicon,
            "Silicon Diode (1N4148)",
            SemiconductorMaterial.Silicon,
            kneeVoltageV: 0.70,
            saturationCurrent300KA: 2.5e-9,
            idealityFactor: 1.7,
            breakdownVoltageV: 50.0,
            BreakdownMechanism.AvalancheImpactIonisation,
            effectiveGapEv: 1.12);

        private static readonly DiodeParameters GermaniumDiode = new DiodeParameters(
            DiodeType.Germanium,
            "Germanium Diode (1N34A)",
            SemiconductorMaterial.Germanium,
            kneeVoltageV: 0.30,
            saturationCurrent300KA: 2.0e-6,
            idealityFactor: 1.3,
            breakdownVoltageV: 40.0,
            BreakdownMechanism.AvalancheImpactIonisation,
            effectiveGapEv: 0.67);

        private static readonly DiodeParameters LedDiode = new DiodeParameters(
            DiodeType.LED,
            "Red/Amber LED",
            SemiconductorMaterial.Silicon,
            kneeVoltageV: 2.0,
            saturationCurrent300KA: 1.0e-20,
            idealityFactor: 1.9,
            breakdownVoltageV: 5.0,
            BreakdownMechanism.AvalancheImpactIonisation,
            effectiveGapEv: 2.1);

        private static readonly DiodeParameters ZenerDiode = new DiodeParameters(
            DiodeType.Zener,
            "Zener Diode (5.1 V)",
            SemiconductorMaterial.Silicon,
            kneeVoltageV: 0.70,
            saturationCurrent300KA: 2.5e-9,
            idealityFactor: 1.65,
            breakdownVoltageV: 5.1,
            BreakdownMechanism.ZenerTunneling,
            effectiveGapEv: 1.12);

        public static MaterialProperties GetMaterialProperties(SemiconductorMaterial material)
        {
            return material == SemiconductorMaterial.Germanium ? GermaniumProps : SiliconProps;
        }

        public static DiodeParameters GetDiodeParameters(DiodeType diodeType)
        {
            switch (diodeType)
            {
                case DiodeType.Germanium:
                    return GermaniumDiode;
                case DiodeType.LED:
                    return LedDiode;
                case DiodeType.Zener:
                    return ZenerDiode;
                default:
                    return SiliconDiode;
            }
        }

        /// <summary>
        /// Thermal voltage V_T = k_B * T / q in Volts (~25.85 mV at 300 K).
        /// </summary>
        public static double ThermalVoltageV(double temperatureK)
        {
            if (temperatureK <= 0.0)
            {
                return 0.0;
            }

            return (BoltzmannConstantJPerK * temperatureK) / ElementaryChargeQ;
        }

        /// <summary>
        /// Intrinsic carrier concentration n_i(T) in cm^-3 anchored at 300 K (0 at 0 K).
        /// </summary>
        public static double IntrinsicConcentrationCm3(SemiconductorMaterial material, double temperatureK)
        {
            if (temperatureK <= 0.0)
            {
                return 0.0;
            }

            MaterialProperties props = GetMaterialProperties(material);
            if (Math.Abs(temperatureK - ReferenceTemperatureK) < 1e-9)
            {
                return props.IntrinsicConcentration300KCm3;
            }

            double tRatio = temperatureK / ReferenceTemperatureK;
            double expArg = -(props.BandGapEv / (2.0 * BoltzmannConstantEvPerK)) *
                            ((1.0 / temperatureK) - (1.0 / ReferenceTemperatureK));

            if (expArg < -700.0)
            {
                return 0.0;
            }

            expArg = Math.Min(expArg, 100.0);
            return props.IntrinsicConcentration300KCm3 * Math.Pow(tRatio, 1.5) * Math.Exp(expArg);
        }

        /// <summary>
        /// Computes equilibrium electron/hole concentrations satisfying charge neutrality and n*p = n_i^2.
        /// At T <= 0 K, returns zero free carriers (carrier freeze-out / insulator).
        /// </summary>
        public static CarrierConcentrations ComputeCarrierConcentrations(
            SemiconductorMaterial material,
            double donorNdCm3,
            double acceptorNaCm3,
            double temperatureK)
        {
            double nd = Math.Max(0.0, donorNdCm3);
            double na = Math.Max(0.0, acceptorNaCm3);
            double ni = IntrinsicConcentrationCm3(material, temperatureK);
            MaterialProperties props = GetMaterialProperties(material);

            if (temperatureK <= 0.0 || ni <= 0.0)
            {
                return new CarrierConcentrations(0.0, 0.0, 0.0, nd, na, 0.0, 0.0);
            }

            double delta = nd - na;
            double n;
            double p;
            double niSq = ni * ni;

            if (Math.Abs(delta) < 1e-12)
            {
                n = ni;
                p = ni;
            }
            else if (delta > 0.0)
            {
                // Avoid catastrophic cancellation when Nd >> ni: compute n via +sqrt, then p = ni^2 / n.
                n = 0.5 * (delta + Math.Sqrt((delta * delta) + (4.0 * niSq)));
                p = niSq / n;
            }
            else
            {
                double absDelta = -delta;
                p = 0.5 * (absDelta + Math.Sqrt((absDelta * absDelta) + (4.0 * niSq)));
                n = niSq / p;
            }

            double vt = ThermalVoltageV(temperatureK);
            double fermiShift = (vt > 0.0 && n > 0.0 && ni > 0.0)
                ? vt * Math.Log(n / ni)
                : 0.0;

            double sigma = ElementaryChargeQ *
                ((n * props.ElectronMobilityCm2Vs) + (p * props.HoleMobilityCm2Vs));

            return new CarrierConcentrations(n, p, ni, nd, na, fermiShift, sigma);
        }

        public static CarrierConcentrations ComputeIntrinsic(SemiconductorMaterial material, double temperatureK)
        {
            return ComputeCarrierConcentrations(material, 0.0, 0.0, temperatureK);
        }

        public static CarrierConcentrations ComputeNType(SemiconductorMaterial material, double donorNdCm3, double temperatureK)
        {
            return ComputeCarrierConcentrations(material, donorNdCm3, 0.0, temperatureK);
        }

        public static CarrierConcentrations ComputePType(SemiconductorMaterial material, double acceptorNaCm3, double temperatureK)
        {
            return ComputeCarrierConcentrations(material, 0.0, acceptorNaCm3, temperatureK);
        }

        /// <summary>
        /// Built-in junction potential V_0 = V_T * ln(N_A * N_D / n_i^2) in Volts.
        /// </summary>
        public static double BuiltInPotentialV(
            SemiconductorMaterial material,
            double acceptorNaCm3,
            double donorNdCm3,
            double temperatureK)
        {
            if (acceptorNaCm3 <= 0.0 || donorNdCm3 <= 0.0 || temperatureK <= 0.0)
            {
                return 0.0;
            }

            double ni = IntrinsicConcentrationCm3(material, temperatureK);
            if (ni <= 0.0)
            {
                return 0.0;
            }

            double vt = ThermalVoltageV(temperatureK);
            double logTerm = Math.Log(acceptorNaCm3) + Math.Log(donorNdCm3) - (2.0 * Math.Log(ni));
            return Math.Max(0.0, vt * logTerm);
        }

        /// <summary>
        /// Abrupt P-N junction electrostatics (V_0, W, x_n, x_p, |E_max|) for applied bias V (V > 0 forward, V < 0 reverse).
        /// </summary>
        public static JunctionElectrostatics ComputeJunctionElectrostatics(
            SemiconductorMaterial material,
            double acceptorNaCm3,
            double donorNdCm3,
            double appliedVoltageV,
            double temperatureK)
        {
            if (acceptorNaCm3 <= 0.0 || donorNdCm3 <= 0.0)
            {
                return new JunctionElectrostatics(0.0, 0.0, 0.0, 0.0, 0.0, 0.0);
            }

            MaterialProperties props = GetMaterialProperties(material);
            double v0 = BuiltInPotentialV(material, acceptorNaCm3, donorNdCm3, temperatureK);
            double effectiveBarrier = Math.Max(0.0, v0 - appliedVoltageV);
            double epsilonCm = props.RelativePermittivity * VacuumPermittivityFPerCm;

            double invSum = (1.0 / acceptorNaCm3) + (1.0 / donorNdCm3);
            double widthCm = Math.Sqrt(((2.0 * epsilonCm * effectiveBarrier) / ElementaryChargeQ) * invSum);

            double totalDoping = acceptorNaCm3 + donorNdCm3;
            double xnCm = widthCm * (acceptorNaCm3 / totalDoping);
            double xpCm = widthCm * (donorNdCm3 / totalDoping);
            double peakField = (ElementaryChargeQ * donorNdCm3 * xnCm) / epsilonCm;

            return new JunctionElectrostatics(v0, effectiveBarrier, widthCm, xnCm, xpCm, peakField);
        }

        /// <summary>
        /// Temperature-scaled reverse saturation current I_s(T) in Amperes.
        /// Scales using (T/300)^3 * exp(-(E_g/(n*k_B))*(1/T - 1/300)), giving ~-2 mV/C forward shift.
        /// </summary>
        public static double DiodeSaturationCurrentA(DiodeType diodeType, double temperatureK)
        {
            if (temperatureK <= 0.0)
            {
                return 0.0;
            }

            DiodeParameters p = GetDiodeParameters(diodeType);
            if (Math.Abs(temperatureK - ReferenceTemperatureK) < 1e-9)
            {
                return p.SaturationCurrent300KA;
            }

            double tRatio = temperatureK / ReferenceTemperatureK;
            double expArg = -(p.EffectiveGapEv / (p.IdealityFactor * BoltzmannConstantEvPerK)) *
                            ((1.0 / temperatureK) - (1.0 / ReferenceTemperatureK));

            if (expArg < -700.0)
            {
                return 0.0;
            }

            expArg = Math.Min(expArg, 80.0);
            return p.SaturationCurrent300KA * Math.Pow(tRatio, 3.0) * Math.Exp(expArg);
        }

        /// <summary>
        /// Ideal Shockley diode current I = I_s * (exp(V / (n * V_T)) - 1) in Amperes.
        /// </summary>
        public static double ShockleyCurrentA(double voltageV, double saturationCurrentA, double idealityFactor, double temperatureK)
        {
            if (temperatureK <= 0.0 || saturationCurrentA <= 0.0 || idealityFactor <= 0.0)
            {
                return 0.0;
            }

            double vt = ThermalVoltageV(temperatureK);
            double expArg = voltageV / (idealityFactor * vt);
            expArg = Math.Max(-80.0, Math.Min(expArg, 60.0));
            return saturationCurrentA * (Math.Exp(expArg) - 1.0);
        }

        /// <summary>
        /// Diode current including reverse breakdown onset for Station 5 and Station 6 I-V plotting.
        /// </summary>
        public static double DiodeCurrentWithBreakdownA(
            DiodeType diodeType,
            double diodeVoltageV,
            double temperatureK,
            double breakdownSlopeConductanceS = 0.25)
        {
            DiodeParameters p = GetDiodeParameters(diodeType);
            double isTemp = DiodeSaturationCurrentA(diodeType, temperatureK);
            double idealCurrent = ShockleyCurrentA(diodeVoltageV, isTemp, p.IdealityFactor, temperatureK);

            if (diodeVoltageV < -p.BreakdownVoltageV)
            {
                double excessReverseV = -diodeVoltageV - p.BreakdownVoltageV;
                double breakdownCurrent = -breakdownSlopeConductanceS * excessReverseV * (1.0 + (2.0 * excessReverseV));
                return idealCurrent + breakdownCurrent;
            }

            return idealCurrent;
        }

        /// <summary>
        /// Forward voltage required to sustain a target forward current according to Shockley equation.
        /// </summary>
        public static double ForwardVoltageAtCurrentV(DiodeType diodeType, double targetCurrentA, double temperatureK)
        {
            if (targetCurrentA <= 0.0 || temperatureK <= 0.0)
            {
                return 0.0;
            }

            DiodeParameters p = GetDiodeParameters(diodeType);
            double isTemp = DiodeSaturationCurrentA(diodeType, temperatureK);
            if (isTemp <= 0.0)
            {
                return p.KneeVoltageV;
            }

            double vt = ThermalVoltageV(temperatureK);
            return p.IdealityFactor * vt * Math.Log((targetCurrentA / isTemp) + 1.0);
        }

        /// <summary>
        /// Solves a series resistor + diode circuit (V_source = V_diode + I * R_series) using bisection.
        /// </summary>
        public static void SolveSeriesDiodeCircuit(
            DiodeType diodeType,
            double sourceVoltageV,
            double seriesResistanceOhms,
            double temperatureK,
            out double diodeVoltageV,
            out double circuitCurrentA)
        {
            double r = Math.Max(1e-3, seriesResistanceOhms);
            if (Math.Abs(sourceVoltageV) < 1e-9)
            {
                diodeVoltageV = 0.0;
                circuitCurrentA = 0.0;
                return;
            }

            double lo = Math.Min(0.0, sourceVoltageV) - 1.0;
            double hi = Math.Max(0.0, sourceVoltageV) + 1.0;

            for (int iter = 0; iter < 80; iter++)
            {
                double mid = 0.5 * (lo + hi);
                double iDiode = DiodeCurrentWithBreakdownA(diodeType, mid, temperatureK);
                double residual = mid + (iDiode * r) - sourceVoltageV;
                if (residual < 0.0)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            diodeVoltageV = 0.5 * (lo + hi);
            circuitCurrentA = (sourceVoltageV - diodeVoltageV) / r;
        }

        /// <summary>
        /// Evaluates abrupt P-N junction spatial profiles (carriers n/p, space-charge rho, E-field,
        /// electrostatic potential V, and band-bending Ec/Ev with quasi-Fermi alignment) from P-side to N-side.
        /// formationProgress01 scales self-consistently from t=0 sharp metallurgical gradient (0) to full equilibrium (1).
        /// </summary>
        public static void SampleJunctionProfile(
            SemiconductorMaterial material,
            double acceptorNaCm3,
            double donorNdCm3,
            double appliedVoltageV,
            double temperatureK,
            double formationProgress01,
            double halfSpanCm,
            JunctionSpatialSample[] outputBuffer)
        {
            if (outputBuffer == null || outputBuffer.Length < 2)
            {
                return;
            }

            double f = Math.Max(0.0, Math.Min(1.0, formationProgress01));
            double span = Math.Max(1e-6, halfSpanCm);
            MaterialProperties props = GetMaterialProperties(material);
            JunctionElectrostatics eq = ComputeJunctionElectrostatics(material, acceptorNaCm3, donorNdCm3, appliedVoltageV, temperatureK);
            CarrierConcentrations pBulk = ComputePType(material, acceptorNaCm3, temperatureK);
            CarrierConcentrations nBulk = ComputeNType(material, donorNdCm3, temperatureK);

            double epsilonCm = props.RelativePermittivity * VacuumPermittivityFPerCm;
            double vt = Math.Max(1e-6, ThermalVoltageV(temperatureK));
            double xpCur = eq.PWidthCm * f;
            double xnCur = eq.NWidthCm * f;
            double barrierCur = eq.EffectiveBarrierV * f * f;

            double eiBulkPEv = -pBulk.FermiShiftFromMidgapEv;
            double halfGapEv = props.BandGapEv * 0.5;
            double efSplitEv = eq.EffectiveBarrierV * (1.0 - (f * f));

            int count = outputBuffer.Length;
            for (int i = 0; i < count; i++)
            {
                double u = i / (double)(count - 1);
                double x = (-1.0 + (2.0 * u)) * span;

                double rho = 0.0;
                double eField = 0.0;
                double potentialV;

                if (xpCur <= 1e-12 && xnCur <= 1e-12)
                {
                    potentialV = 0.0;
                }
                else if (x < -xpCur)
                {
                    potentialV = 0.0;
                }
                else if (x < 0.0)
                {
                    rho = -ElementaryChargeQ * acceptorNaCm3;
                    double dx = x + xpCur;
                    eField = -(ElementaryChargeQ * acceptorNaCm3 * dx) / epsilonCm;
                    potentialV = (ElementaryChargeQ * acceptorNaCm3 * dx * dx) / (2.0 * epsilonCm);
                }
                else if (x <= xnCur)
                {
                    rho = ElementaryChargeQ * donorNdCm3;
                    double dx = xnCur - x;
                    eField = -(ElementaryChargeQ * donorNdCm3 * dx) / epsilonCm;
                    potentialV = barrierCur - ((ElementaryChargeQ * donorNdCm3 * dx * dx) / (2.0 * epsilonCm));
                }
                else
                {
                    potentialV = barrierCur;
                }

                double pFull = ClampDouble(pBulk.HolesCm3 * Math.Exp(-potentialV / vt), Math.Max(1.0, nBulk.HolesCm3), pBulk.HolesCm3);
                double nFull = ClampDouble(nBulk.ElectronsCm3 * Math.Exp(-(barrierCur - potentialV) / vt), Math.Max(1.0, pBulk.ElectronsCm3), nBulk.ElectronsCm3);

                double stepP = x < 0.0 ? pBulk.HolesCm3 : Math.Max(1.0, nBulk.HolesCm3);
                double stepN = x < 0.0 ? Math.Max(1.0, pBulk.ElectronsCm3) : nBulk.ElectronsCm3;

                // Blend smoothly in log-carrier space from t=0 sharp step to Boltzmann equilibrium profile.
                double logP = ((1.0 - f) * Math.Log10(stepP)) + (f * Math.Log10(pFull));
                double logN = ((1.0 - f) * Math.Log10(stepN)) + (f * Math.Log10(nFull));
                double pVal = Math.Pow(10.0, logP);
                double nVal = Math.Pow(10.0, logN);

                double eiLocal = eiBulkPEv - potentialV;
                double ecEv = eiLocal + halfGapEv;
                double evEv = eiLocal - halfGapEv;
                double efHoleEv = 0.0;
                double efElectronEv = x < 0.0 ? efSplitEv * 0.15 : efSplitEv;
                if (f > 0.999)
                {
                    efElectronEv = 0.0;
                }

                outputBuffer[i] = new JunctionSpatialSample(
                    x, nVal, pVal, rho, eField, potentialV, ecEv, evEv, efElectronEv, efHoleEv);
            }
        }

        private static double ClampDouble(double v, double min, double max)
        {
            if (v < min) return min;
            return v > max ? max : v;
        }
    }
}
