from __future__ import annotations

import subprocess
from dataclasses import dataclass


MYSQL_HOST = "127.0.0.1"
MYSQL_USER = "root"
MYSQL_PASSWORD = "123123"
MYSQL_DB = "villacarmen"


@dataclass(frozen=True)
class BookingRow:
    id: int
    customer_name: str
    contact_phone: str
    reservation_date: str  # yyyy-mm-dd
    reservation_time_hhmm: str  # HH:mm
    party_size: int
    arroz_type: str | None
    arroz_servings: int | None
    status: str


def _mysql_query_tsv(sql: str) -> list[list[str]]:
    """
    Execute SQL via the mysql CLI and return rows as list of fields.
    Uses -N -B for raw tab-separated output with no header.
    """
    cmd = [
        "mysql",
        "-h",
        MYSQL_HOST,
        f"-u{MYSQL_USER}",
        f"-p{MYSQL_PASSWORD}",
        "-D",
        MYSQL_DB,
        "-N",
        "-B",
        "-e",
        sql,
    ]
    out = subprocess.check_output(cmd, text=True)
    out = out.strip("\n")
    if not out:
        return []
    rows = []
    for line in out.splitlines():
        rows.append(line.split("\t"))
    return rows


def find_booking_by_phone_date_time(
    *,
    phone_last9: str,
    db_date: str,  # yyyy-mm-dd
    db_time_hhmm: str,  # HH:mm
) -> list[BookingRow]:
    sql = f"""
SELECT
  id,
  customer_name,
  contact_phone,
  reservation_date,
  TIME_FORMAT(reservation_time, '%H:%i') AS reservation_time_hhmm,
  party_size,
  JSON_UNQUOTE(JSON_EXTRACT(arroz_type, '$[0]')) AS arroz_type,
  CAST(JSON_EXTRACT(arroz_servings, '$[0]') AS UNSIGNED) AS arroz_servings,
  status
FROM bookings
WHERE contact_phone = '{phone_last9}'
  AND reservation_date = '{db_date}'
  AND TIME_FORMAT(reservation_time, '%H:%i') = '{db_time_hhmm}'
ORDER BY id DESC;
""".strip()

    rows = _mysql_query_tsv(sql)
    result: list[BookingRow] = []
    for r in rows:
        result.append(
            BookingRow(
                id=int(r[0]),
                customer_name=r[1],
                contact_phone=r[2],
                reservation_date=r[3],
                reservation_time_hhmm=r[4],
                party_size=int(r[5]),
                arroz_type=(r[6] if r[6] != "NULL" and r[6] != "" else None),
                arroz_servings=(int(r[7]) if r[7] != "NULL" and r[7] != "" else None),
                status=r[8],
            )
        )
    return result


def assert_booking_inserted(
    *,
    phone_last9: str,
    db_date: str,
    db_time_hhmm: str,
    expected_party_size: int,
    expected_status: str = "pending",
    expected_arroz_type: str | None = None,
    expected_arroz_servings: int | None = None,
) -> BookingRow:
    rows = find_booking_by_phone_date_time(
        phone_last9=phone_last9, db_date=db_date, db_time_hhmm=db_time_hhmm
    )
    if not rows:
        raise AssertionError(
            f"Expected booking to be inserted but none found for phone={phone_last9}, date={db_date}, time={db_time_hhmm}"
        )

    # If multiple, validate the latest one.
    row = rows[0]

    if row.party_size != expected_party_size:
        raise AssertionError(f"Expected party_size={expected_party_size}, got {row.party_size} (id={row.id})")

    if row.status != expected_status:
        raise AssertionError(f"Expected status='{expected_status}', got '{row.status}' (id={row.id})")

    if expected_arroz_type is None:
        if row.arroz_type is not None:
            raise AssertionError(f"Expected arroz_type NULL, got '{row.arroz_type}' (id={row.id})")
        if row.arroz_servings is not None:
            raise AssertionError(f"Expected arroz_servings NULL, got {row.arroz_servings} (id={row.id})")
    else:
        if row.arroz_type != expected_arroz_type:
            raise AssertionError(f"Expected arroz_type='{expected_arroz_type}', got '{row.arroz_type}' (id={row.id})")
        if expected_arroz_servings is not None and row.arroz_servings != expected_arroz_servings:
            raise AssertionError(
                f"Expected arroz_servings={expected_arroz_servings}, got {row.arroz_servings} (id={row.id})"
            )

    return row


def assert_no_booking_inserted(
    *,
    phone_last9: str,
    db_date: str,
    db_time_hhmm: str,
) -> None:
    rows = find_booking_by_phone_date_time(
        phone_last9=phone_last9, db_date=db_date, db_time_hhmm=db_time_hhmm
    )
    if rows:
        raise AssertionError(
            f"Expected NO booking insert, but found {len(rows)} row(s). Latest id={rows[0].id} for phone={phone_last9}, date={db_date}, time={db_time_hhmm}"
        )


