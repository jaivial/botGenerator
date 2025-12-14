"""
Booking scenario matrix (A–E) for automated tests.

These are higher-level scenarios that use DB + WhatsApp assertions.
They are executed by run_booking_matrix_tests.py, not the simple text-only runner.
"""

from __future__ import annotations

from dataclasses import dataclass


@dataclass(frozen=True)
class Scenario:
    key: str
    name: str
    category: str
    expect_insert: bool
    party_size: int
    wants_rice: bool | None  # None means we will trigger a rice failure path
    rice_type: str | None = None
    rice_servings: int | None = None


def get_booking_matrix_scenarios() -> list[Scenario]:
    """
    A–E categories:
    - A: happy paths (insert)
    - B: intentional failures (no insert)
    - C: abandonment (no insert)
    - D: topic switching (insert/no insert depending on completion)
    - E: adversarial/messy inputs (mostly no insert, or insert with last-value-wins)
    """
    return [
        # A) Happy paths
        Scenario(
            key="A1",
            name="A1 Basic booking, no rice",
            category="A",
            expect_insert=True,
            party_size=2,
            wants_rice=False,
        ),
        Scenario(
            key="A2",
            name="A2 Booking with valid rice + servings",
            category="A",
            expect_insert=True,
            party_size=2,
            wants_rice=True,
            rice_type="Arroz de chorizo",
            rice_servings=2,
        ),
        Scenario(
            key="A3",
            name="A3 Rice-first then rest (still inserts)",
            category="A",
            expect_insert=True,
            party_size=4,
            wants_rice=True,
            rice_type="Arroz meloso de pulpo y gambones",
            rice_servings=2,
        ),
        Scenario(
            key="A4",
            name="A4 Extras yes then count (tronas/carritos)",
            category="A",
            expect_insert=True,
            party_size=3,
            wants_rice=True,
            rice_type="Arroz a banda",
            rice_servings=2,
        ),
        # B) Failures / no insert
        Scenario(
            key="B1",
            name="B1 Invalid rice (menu list, no insert)",
            category="B",
            expect_insert=False,
            party_size=2,
            wants_rice=None,
            rice_type="Arroz de pollo",
            rice_servings=2,
        ),
        Scenario(
            key="B2",
            name="B2 Ambiguous rice (multiple options, no insert)",
            category="B",
            expect_insert=False,
            party_size=2,
            wants_rice=None,
            rice_type="carrillada con boletus",
            rice_servings=2,
        ),
        Scenario(
            key="B3",
            name="B3 Rice servings <2 (no insert)",
            category="B",
            expect_insert=False,
            party_size=2,
            wants_rice=True,
            rice_type="Arroz de señoret",
            rice_servings=1,
        ),
        # C) Abandonment
        Scenario(
            key="C1",
            name="C1 Abort mid-path (no insert)",
            category="C",
            expect_insert=False,
            party_size=2,
            wants_rice=None,
        ),
        Scenario(
            key="C2",
            name="C2 Abrupt stop after data request (no insert)",
            category="C",
            expect_insert=False,
            party_size=2,
            wants_rice=None,
        ),
        # D) Topic switching
        Scenario(
            key="D1",
            name="D1 Switch topic mid-booking then resume (insert)",
            category="D",
            expect_insert=True,
            party_size=2,
            wants_rice=False,
        ),
        Scenario(
            key="D2",
            name="D2 Switch topic and never resume (no insert)",
            category="D",
            expect_insert=False,
            party_size=2,
            wants_rice=None,
        ),
        # E) Adversarial / messy inputs
        Scenario(
            key="E1",
            name="E1 Contradictions: change time/people, last value wins (insert)",
            category="E",
            expect_insert=True,
            party_size=4,
            wants_rice=False,
        ),
        Scenario(
            key="E2",
            name="E2 Prompt injection attempt (no insert)",
            category="E",
            expect_insert=False,
            party_size=2,
            wants_rice=None,
        ),
    ]


