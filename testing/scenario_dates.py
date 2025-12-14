from __future__ import annotations

from dataclasses import dataclass
from datetime import date, timedelta


@dataclass(frozen=True)
class ScenarioDateTime:
    """Reservation date/time for a scenario."""

    # dd/MM/yyyy (what the bot expects)
    user_date: str
    # HH:mm (what the bot expects)
    user_time: str
    # yyyy-MM-dd (what the DB stores)
    db_date: str
    # HH:mm (for DB comparisons via TIME_FORMAT)
    db_time_hhmm: str


def _format_user_date(d: date) -> str:
    return d.strftime("%d/%m/%Y")


def _format_db_date(d: date) -> str:
    return d.strftime("%Y-%m-%d")


def get_scenario_datetime(
    scenario_index: int,
    *,
    base_date: date | None = None,
    base_week_offset_days: int = 365,
) -> ScenarioDateTime:
    """
    Deterministically generate a unique (open-day) reservation date/time per scenario.

    - Uses Saturdays to avoid closed-day rules.
    - Offsets by weeks to avoid collisions when reusing the same phone and keeping DB rows.
    - Uses one of the default "active hours" that the availability service can generate:
      13:30, 14:00, 14:30, 15:00.
    """
    if scenario_index < 0:
        raise ValueError("scenario_index must be >= 0")

    today = date.today()
    base_date = base_date or today

    # Start sufficiently in the future to avoid any special closures configured for near-term dates.
    start = base_date + timedelta(days=base_week_offset_days)

    # Move to next Saturday (weekday: Mon=0..Sun=6; Sat=5)
    days_until_sat = (5 - start.weekday()) % 7
    next_saturday = start + timedelta(days=days_until_sat)

    d = next_saturday + timedelta(days=7 * scenario_index)

    # Prefer safer hours to reduce collisions with existing bookings (we keep DB rows).
    # Avoid 14:00 since it often gets rejected by availability in this dataset.
    allowed_hours = ["14:30", "15:00"]
    t = allowed_hours[scenario_index % len(allowed_hours)]

    return ScenarioDateTime(
        user_date=_format_user_date(d),
        user_time=t,
        db_date=_format_db_date(d),
        db_time_hhmm=t,
    )


