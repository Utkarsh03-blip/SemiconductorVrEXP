# P-N Junction VR Lab — Authoritative Science Reference

This document is the authoritative scientific reference for `PNJunctionLab`. All formulas, physical constants, and educational claims in `SemiconductorModel` (pure C#, no `UnityEngine` dependency) and all lab stations must match this document. Visual scripts must read values from `SemiconductorModel` rather than hard-coding physics numbers.

> **Visual Scale Rule:** All atomic, crystal-lattice, and microscopic carrier visuals are schematic and must display a visible **"Not to scale"** label.

---

## 1. Fundamental Constants (SI & Semiconductor Units)

| Symbol | Quantity | Value |
| :--- | :--- | :--- |
| $q$ | Elementary charge | $1.602176634 \times 10^{-19}\text{ C}$ |
| $k_B$ | Boltzmann constant | $1.380649 \times 10^{-23}\text{ J/K} \approx 8.617333 \times 10^{-5}\text{ eV/K}$ |
| $\varepsilon_0$ | Vacuum permittivity | $8.854187817 \times 10^{-12}\text{ F/m} = 8.854187817 \times 10^{-14}\text{ F/cm}$ |
| $V_T$ | Thermal voltage ($k_B T / q$) at $300\text{ K}$ | $\approx 25.85\text{ mV}$ ($\approx 25.9\text{ mV}$) |

---

## 2. Intrinsic Semiconductors

* **Materials:** Pure Silicon ($\text{Si}$) and Germanium ($\text{Ge}$), Group IV elements with **4 valence electrons** forming a covalent **diamond crystal lattice**.
* **At $0\text{ K}$:** All covalent bonds are intact; there are no free charge carriers, so the pure semiconductor behaves as an **insulator**.
* **Thermal Generation & Recombination ($T > 0\text{ K}$):** Thermal energy breaks covalent bonds, generating equal numbers of free **electrons** (in the conduction band) and **holes** (vacancies in the valence band). Electrons and holes also continuously **recombine**. At thermal equilibrium, generation rate equals recombination rate:
  $$n = p = n_i$$
* **Band Gap ($E_g$ at $300\text{ K}$):**
  * Silicon ($\text{Si}$): $E_g = 1.12\text{ eV}$
  * Germanium ($\text{Ge}$): $E_g = 0.67\text{ eV}$
* **Intrinsic Carrier Concentration ($n_i$ at $300\text{ K}$):**
  * Silicon ($\text{Si}$): $n_i \approx 1.5 \times 10^{10}\text{ cm}^{-3}$
  * Germanium ($\text{Ge}$): $n_i \approx 2.4 \times 10^{13}\text{ cm}^{-3}$
* **Conductivity vs. Temperature:** Conductivity rises strongly with temperature because $n_i$ increases exponentially as thermal energy breaks more bonds (unlike metals, where higher temperature usually reduces conductivity due to lattice scattering).

---

## 3. Extrinsic Semiconductors (Doping)

Typical intentional doping concentration is $10^{14}\text{ to }10^{18}\text{ cm}^{-3}$ (roughly $1$ dopant atom per $10^6\text{ to }10^8$ host $\text{Si}$ atoms). **Both n-type and p-type semiconductors remain electrically neutral overall.**

### Mass-Action Law
At thermal equilibrium (non-degenerate doping):
$$n \cdot p = n_i^2$$

### N-Type (Donor Doping)
* **Dopants:** Pentavalent (Group V) elements — Phosphorus ($\text{P}$), Arsenic ($\text{As}$), Antimony ($\text{Sb}$).
* **Mechanism:** Four valence electrons form covalent bonds with neighbouring host atoms; the fifth electron is loosely bound and becomes free at room temperature.
* **Energy Level:** Donor level ($E_D$) sits just below the conduction band ($\sim 0.045\text{ eV}$ below $E_c$ for $\text{P}$ in $\text{Si}$).
* **Charges Present:** Mobile free **electrons** ($-$) + **fixed positive donor ions** ($+$).
* **Carriers (when $N_D \gg n_i$):**
  * Majority carriers: **electrons**, $n \approx N_D$
  * Minority carriers: **holes**, $p = \dfrac{n_i^2}{n} \approx \dfrac{n_i^2}{N_D}$
* **Fermi Level:** $E_F$ shifts upward toward the conduction band ($E_c$).

### P-Type (Acceptor Doping)
* **Dopants:** Trivalent (Group III) elements — Boron ($\text{B}$), Aluminium ($\text{Al}$), Gallium ($\text{Ga}$), Indium ($\text{In}$).
* **Mechanism:** With only 3 valence electrons, one covalent bond is incomplete, creating a **hole**. Neighbouring valence electrons can hop into the vacancy, making the hole behave as a mobile positive charge carrier.
* **Energy Level:** Acceptor level ($E_A$) sits just above the valence band ($E_v$).
* **Charges Present:** Mobile free **holes** ($+$) + **fixed negative acceptor ions** ($-$).
* **Carriers (when $N_A \gg n_i$):**
  * Majority carriers: **holes**, $p \approx N_A$
  * Minority carriers: **electrons**, $n = \dfrac{n_i^2}{p} \approx \dfrac{n_i^2}{N_A}$
* **Fermi Level:** $E_F$ shifts downward toward the valence band ($E_v$).

---

## 4. P-N Junction at Equilibrium ($V = 0$)

* **Diffusion:** When P-type and N-type regions meet, steep concentration gradients cause electrons to diffuse from $\text{N} \to \text{P}$ and holes from $\text{P} \to \text{N}$, where they recombine near the metallurgical junction.
* **Depletion Region (Space-Charge Region):** Recombination leaves behind uncovered, immobile charged lattice ions: **positive donor ions ($+$)** on the N side and **negative acceptor ions ($-$)** on the P side, with virtually no free carriers inside this region.
* **Electric Field & Drift:** The uncovered space charge establishes an internal electric field pointing from **$\text{N} \to \text{P}$**. This field produces a **drift current** of minority carriers that opposes diffusion.
* **Equilibrium:** Drift and diffusion balance exactly; **net current $= 0$**, depletion width $W$ is stable, the **Fermi level ($E_F$) is flat** across the entire junction, and the energy bands bend by $qV_0$.

### Equilibrium Equations (Abrupt Junction)
* **Built-in Potential ($V_0$):**
  $$V_0 = V_T \ln\left(\frac{N_A N_D}{n_i^2}\right)$$
  Typical values at $300\text{ K}$: $\text{Si} \approx 0.7\text{ V}$, $\text{Ge} \approx 0.3\text{ V}$.
* **Depletion Width ($W$):**
  $$W = \sqrt{\frac{2\varepsilon (V_0 - V)}{q}\left(\frac{1}{N_A} + \frac{1}{N_D}\right)}$$
  where $\varepsilon = \varepsilon_r \varepsilon_0$ and $V$ is applied forward voltage ($V > 0$ forward, $V < 0$ reverse).
* **Asymmetric Penetration:** The depletion region extends further into the **lighter-doped** side:
  $$x_n = W \frac{N_A}{N_A + N_D}, \qquad x_p = W \frac{N_D}{N_A + N_D}$$
* **Reference Verification Case:** Silicon at $300\text{ K}$ with $N_A = N_D = 10^{16}\text{ cm}^{-3}$ and $n_i = 1.5 \times 10^{10}\text{ cm}^{-3}$:
  * $V_0 \approx 0.69\text{ V}$ ($0.691\text{ V}$)
  * $W \approx 0.4\text{ }\mu\text{m}$ ($\approx 0.42\text{ }\mu\text{m}$ with $\varepsilon_r = 11.7$)

---

## 5. Forward and Reverse Bias & Diode I-V Characteristic

### Forward Bias ($V > 0$: Positive terminal on P, Negative on N)
* The external electric field opposes the built-in field.
* Potential barrier drops to $V_0 - V$, and the depletion width $W$ narrows.
* Majority carriers easily cross the junction; current increases exponentially according to the **Shockley diode equation**:
  $$I = I_s \left(\exp\left(\frac{V}{n V_T}\right) - 1\right), \qquad n \in [1, 2]$$
* **Typical Knee / Forward Voltages:**
  * Silicon diode: $\approx 0.7\text{ V}$
  * Germanium diode: $\approx 0.3\text{ V}$
  * LED: $\approx 1.8\text{ to }3.0\text{ V}$
* **Series Resistor:** Required in practical circuits to limit current once $V$ approaches/exceeds the knee voltage.
* **Temperature Effect:** Forward voltage at fixed current drops by approximately **$2\text{ mV}/^\circ\text{C}$**.

### Reverse Bias ($V = -V_R < 0$: Negative terminal on P, Positive on N)
* The external field reinforces the built-in field.
* Potential barrier rises to $V_0 + V_R$, and the depletion width $W$ widens.
* Majority carrier diffusion is suppressed; only a tiny **reverse saturation current ($I_s$)** due to thermally generated minority carriers flows, and $I_s$ increases with temperature.
* **Reverse Breakdown:**
  * **Zener breakdown:** Occurs in heavily doped junctions at lower reverse voltages via quantum mechanical **tunnelling**.
  * **Avalanche breakdown:** Occurs in more lightly doped junctions at higher reverse voltages via **impact ionisation**.

---

## 6. Explicit Model Assumptions (for `SemiconductorModel` Implementation)

Where `MASTER_PROMPT.md` gives reference values at $300\text{ K}$ rather than full parametric temperature/device curves, `SemiconductorModel` uses the following explicitly documented engineering assumptions:

1. **Relative Permittivity ($\varepsilon_r$):** $\text{Si}: 11.7$, $\text{Ge}: 16.0$, LED (representative $\text{GaAsP}$/$\text{AlGaInP}$): $12.9$ (representative).
2. **Host Lattice Atomic Density:** $\text{Si}: 5.0 \times 10^{22}\text{ cm}^{-3}$, $\text{Ge}: 4.42 \times 10^{22}\text{ cm}^{-3}$ (for showing approximate dopant-to-host ratios).
3. **Temperature Scaling of $n_i(T)$:** Anchored directly to the authoritative $300\text{ K}$ values ($n_{i,300}(\text{Si}) = 1.5 \times 10^{10}\text{ cm}^{-3}$, $n_{i,300}(\text{Ge}) = 2.4 \times 10^{13}\text{ cm}^{-3}$) using the standard $T^{3/2} \exp(-E_g / (2 k_B T))$ ratio relative to $T_0 = 300\text{ K}$ (with $n_i(0\text{ K}) = 0$):
   $$n_i(T) = n_{i,300} \left(\frac{T}{300}\right)^{3/2} \exp\left(-\frac{E_g}{2 k_B}\left(\frac{1}{T} - \frac{1}{300}\right)\right)$$
   At very low temperature where $T \to 0\text{ K}$, $n_i \to 0$.
4. **General Equilibrium Carrier Concentrations (Arbitrary Doping & $T$):** Using charge neutrality ($p - n + N_D - N_A = 0$) combined with $np = n_i^2$ when $n_i > 0$, reducing smoothly to $n \approx N_D, p = n_i^2/N_D$ (n-type) and $p \approx N_A, n = n_i^2/N_A$ (p-type) in the extrinsic regime, and $n = p = n_i$ when intrinsic ($N_D = N_A = 0$). At $T \le 0\text{ K}$, carrier freeze-out is assumed ($n = p = 0$).
5. **Diode Presets for Station 5 & Station 6:** Representative device parameter sets use $I_s(T) = I_{s,300} (T/300)^3 \exp\left(-\frac{E_{g,\text{eff}}}{n k_B}\left(\frac{1}{T} - \frac{1}{300}\right)\right)$, breakdown voltage $V_{BR}$ and breakdown mechanism label (Zener tunnelling vs Avalanche impact ionisation), yielding rising $I_s(T)$ and $\approx -2\text{ mV}/^\circ\text{C}$ forward-voltage shift at constant current (verified by EditMode tests).
6. **Station 1 Intrinsic Conductivity Display:** Relative/SI conductivity $\sigma = q (n \mu_n + p \mu_p)$ using standard $300\text{ K}$ carrier mobilities ($\text{Si}: \mu_n = 1350$, $\mu_p = 480\text{ cm}^2/\text{V}\cdot\text{s}$; $\text{Ge}: \mu_n = 3900$, $\mu_p = 1900\text{ cm}^2/\text{V}\cdot\text{s}$) with a clear note when lattice-scattering vs carrier-concentration dominance is displayed.
7. **Stations 2 & 3 Extrinsic Room-Temperature & Acceptor Energy Assumptions:** Extrinsic doping benches evaluate $n$, $p = n_i^2/n$ (or $p, n = n_i^2/p$), $E_F - E_i$, and $\sigma$ at nominal room temperature **$T = 300\text{ K}$** with full dopant ionization across $N_D, N_A \in [10^{14}, 10^{18}]\text{ cm}^{-3}$, and display a representative Boron shallow acceptor ionization offset **$E_a \approx 0.045\text{ eV}$ above $E_v$** (alongside donor level $E_d \approx 0.045\text{ eV}$ below $E_c$) on `ExtrinsicBandDiagramView`.
8. **Station 4 Equilibrium Junction Formation Profile Conventions:** Spatial plots ($n/p$, $\rho$, $E$, $V$) and band-bending diagrams use the abrupt depletion approximation at nominal room temperature **$T = 300\text{ K}$** across a symmetric window $[-L, +L]$ with $L = \max(1.0\ \mu\text{m}, 1.35 \cdot \max(x_p, x_n))$, self-consistently parameterizing intermediate time-lapse stages $f \in [0, 1]$ with $x_{p,\text{cur}} = f x_p$, $x_{n,\text{cur}} = f x_n$, $E_{\text{peak}}(f) = -f |E_{\max}|$, and $V_{\text{barrier}}(f) = f^2 V_0$ while quasi-Fermi levels converge flat to $E_F$ at $f = 1$.
9. **Stations 5 & 6 Circuit Load & Analyzer Sweep Conventions:**
   * **Station 5 Biasing Circuit:** Uses a representative Silicon diode ($V_{\text{knee}} = 0.70\text{ V}$, $I_{s,300} = 2.5\text{ nA}$, $n = 1.7$, $V_{BR} = 50.0\text{ V}$ Avalanche) at $T = 300\text{ K}$ (representative junction doping $N_A = N_D = 10^{16}\text{ cm}^{-3}$ for depletion scaling) in series with a **$R = 220\ \Omega$ protective series resistor** solved via `SemiconductorModel.SolveSeriesDiodeCircuit`, over a piecewise battery sweep $V_{\text{src}} \in [0, 60]\text{ V}$ (`0..5 V` fine control + `5..60 V` reverse breakdown sweep).
   * **Station 6 I-V Characteristic Analyzer:** Evaluates all 4 diode presets (`Silicon`: $V_{BR} = 50\text{ V}$, `Germanium`: $V_{BR} = 40\text{ V}$, `LED`: $E_{g,\text{eff}} = 2.1\text{ eV}, V_{\text{knee}} \approx 2.0\text{ V}, V_{BR} = 5.0\text{ V}$, `Zener`: $V_{BR} = 5.1\text{ V}$) across $V_{\text{src}} \in [-6.5\text{ V}, +3.5\text{ V}]$ with a **$R = 100\ \Omega$ series resistor** and chamber temperature $T \in [220\text{ K}, 420\text{ K}]$, plus an optional zero-resistance **Ideal Shockley Overlay** and a logarithmic micro-current **Reverse Leakage Inset Zoom**.
