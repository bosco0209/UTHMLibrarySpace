"""One-off migration: replace the 12 demo rooms with the 9 real PTTA rooms in librarysp.db.

Adds the two display-only columns (CapacityMin, Units), rewrites the facility rows, re-points
the existing bookings / feedback to the equivalent new room, then removes the leftover rows.
A backup of the database is written to App_Data/librarysp.db.bak before anything is changed.
"""
import os
import shutil
import sqlite3

ROOT = r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace'
DB = os.path.join(ROOT, 'librarysp.db')
BACKUP = os.path.join(ROOT, 'App_Data', 'librarysp.db.bak')

IMG_STUDY2 = 'https://images.pexels.com/photos/9572540/pexels-photo-9572540.jpeg?auto=compress&cs=tinysrgb&w=1200'
IMG_STUDY4 = 'https://images.pexels.com/photos/6549372/pexels-photo-6549372.jpeg?auto=compress&cs=tinysrgb&w=1200'
IMG_MEET1 = 'https://images.unsplash.com/photo-1628062699790-7c45262b82b4?auto=format&fit=crop&w=1200&q=80'
IMG_MEET2 = 'https://images.unsplash.com/photo-1497366811353-6870744d04b2?auto=format&fit=crop&w=1200&q=80'
IMG_TUT1 = 'https://images.unsplash.com/photo-1541829070764-84a7d30dd3f3?auto=format&fit=crop&w=1200&q=80'
IMG_TUT2 = 'https://images.unsplash.com/photo-1718327453695-4d32b94c90a4?auto=format&fit=crop&w=1200&q=80'
IMG_HALL1 = 'https://images.unsplash.com/photo-1763706320063-b210731b37a1?auto=format&fit=crop&w=1200&q=80'
IMG_TECH1 = 'https://images.unsplash.com/photo-1606761568499-6d2451b23c66?auto=format&fit=crop&w=1200&q=80'
IMG_TECH2 = 'https://images.unsplash.com/photo-1776397409772-6202fe558465?auto=format&fit=crop&w=1200&q=80'

# id -> (Code, Name, Type, Capacity, CapacityMin, Units, Location, Description, Amenities, ImageUrl)
ROOMS = {
    1: ('SR-01', "Bilik Iqra'", 'Study', 1, None, 118, 'ARAS 2 & 4, PTTA',
        'Karel Pembacaan Khusus — ruang bacaan karel persendirian (RM20/bulan). 118 karel disediakan.',
        'Karel persendirian,Lampu meja,Plug kuasa,WiFi,Berhawa dingin', IMG_STUDY4),
    4: ('MR-03', 'Bilik Hikmah', 'Meeting', 6, 4, 20, 'ARAS 2, PTTA',
        'Bilik Perbincangan Kumpulan — dilengkapi skrin paparan & papan putih.',
        'Skrin paparan,Papan putih,Plug kuasa,WiFi,Berhawa dingin', IMG_STUDY2),
    5: ('MR-01', 'Lestari Lounge', 'Meeting', 20, None, 1, 'ARAS 3, PTTA',
        'Ruang Santai & Perbincangan — ruang santai eksklusif pembelajaran kolaboratif.',
        'Sofa santai,Skrin paparan,WiFi,Berhawa dingin', IMG_MEET2),
    6: ('MR-02', 'Bilik Eksplorasi', 'Meeting', 10, 6, 20, 'ARAS 3, PTTA',
        'Bilik Perbincangan Mini Seminar — sesuai untuk perbincangan kumpulan berskala sederhana.',
        'Skrin paparan,Papan putih,Mikrofon,WiFi,Berhawa dingin', IMG_MEET1),
    7: ('TR-01', 'Bilik Lestari 1', 'Tutorial', 80, None, 1, 'ARAS 3, PTTA',
        'Bilik Seminar Utama — bilik seminar rasmi untuk bengkel & taklimat.',
        'Projektor,Mikrofon,Sistem bunyi,Papan putih,WiFi,Berhawa dingin', IMG_TUT1),
    8: ('TR-02', 'Bilik Lestari 2', 'Tutorial', 80, None, 1, 'ARAS 3, PTTA',
        'Bilik Seminar Utama — bilik seminar serbaguna berhawa dingin.',
        'Projektor,Mikrofon,Papan putih,WiFi,Berhawa dingin', IMG_TUT2),
    9: ('HL-01', 'Auditorium AI-Jazari', 'Hall', 150, None, 1, 'ARAS 3, PTTA',
        'Auditorium Utama PTTA — dewan persembahan & persidangan berprestij.',
        'Pentas,Projektor dwi,Sistem PA,Mikrofon tanpa wayar,Rakaman', IMG_HALL1),
    11: ('TC-01', 'Makmal A.I. UTHM', 'Technology', 40, 30, 1, 'ARAS 2, PTTA',
         'Makmal Komputer & A.I. — stesen kerja berkuasa tinggi khas A.I.',
         'Stesen kerja GPU,Perisian A.I.,Projektor,WiFi,Penghawa dingin', IMG_TECH1),
    12: ('TC-02', 'Remote Pilot Smart Classroom', 'Technology', 30, 20, 1, 'ARAS 2, PTTA',
         'Bilik Pengajaran Pintar — fasiliti pengajaran interaktif & multimedia.',
         'Skrin interaktif,Kamera PTZ,Mikrofon,WiFi,Berhawa dingin', IMG_TECH2),
}

# old room id -> new room id (bookings and feedback keep pointing at the equivalent room)
BOOKING_REMAP = {2: 1, 3: 1, 10: 9}
DELETE_IDS = (2, 3, 10)

TEXT_REPLACEMENTS = [
    ('Study Pod Cendana', "Bilik Iqra'"),
    ('Study Pod Meranti', "Bilik Iqra'"),
    ('Group Study Room Jati', "Bilik Iqra'"),
    ('Quiet Carrel Merbau', "Bilik Iqra'"),
    ('Meeting Room Seri Gading', 'Lestari Lounge'),
    ('Meeting Room Parit Raja', 'Bilik Eksplorasi'),
    ('Tutorial Room Tun Aminah 1', 'Bilik Lestari 1'),
    ('Tutorial Room Tun Aminah 2', 'Bilik Lestari 2'),
    ('Dewan Ilmu', 'Auditorium AI-Jazari'),
    ('Auditorium Bestari', 'Auditorium AI-Jazari'),
    ('Digital Media Lab', 'Makmal A.I. UTHM'),
    ('Immersive VR Studio', 'Remote Pilot Smart Classroom'),
    ('Study Rooms (RM10, 1-month rental, instant)', "Bilik Iqra' carrels (RM20, 1-month rental, instant)"),
    ('RM10.00', 'RM20.00'),
    ('RM10', 'RM20'),
]


def main():
    shutil.copy2(DB, BACKUP)
    print('backup written ->', BACKUP)

    con = sqlite3.connect(DB)
    cur = con.cursor()
    cur.execute('PRAGMA foreign_keys=OFF')

    cols = [r[1] for r in cur.execute('PRAGMA table_info(Facilities)')]
    if 'CapacityMin' not in cols:
        cur.execute('ALTER TABLE Facilities ADD COLUMN CapacityMin INTEGER NULL')
    if 'Units' not in cols:
        cur.execute('ALTER TABLE Facilities ADD COLUMN Units INTEGER NOT NULL DEFAULT 1')

    for fid, (code, name, typ, cap, capmin, units, loc, desc, amen, img) in ROOMS.items():
        cur.execute(
            '''UPDATE Facilities SET Code=?, Name=?, Type=?, Capacity=?, CapacityMin=?, Units=?,
                   Location=?, Description=?, Amenities=?, ImageUrl=?, Status='Available'
               WHERE Id=?''',
            (code, name, typ, cap, capmin, units, loc, desc, amen, img, fid))

    # Re-point existing bookings / feedback at the equivalent new room.
    for old, new in BOOKING_REMAP.items():
        cur.execute('UPDATE Bookings SET FacilityId=? WHERE FacilityId=?', (new, old))
        cur.execute('UPDATE Feedbacks SET FacilityId=? WHERE FacilityId=?', (new, old))

    # Carrel rentals are RM20 a month now — keep the stored amount consistent with the new fee.
    cur.execute("UPDATE Bookings SET Fee=20.0 WHERE FacilityId=1 AND Fee>0")

    placeholders = ','.join('?' * len(DELETE_IDS))
    cur.execute(f'DELETE FROM Facilities WHERE Id IN ({placeholders})', DELETE_IDS)

    for old, new in TEXT_REPLACEMENTS:
        for table, column in (('Notifications', 'Title'), ('Notifications', 'Message'),
                              ('Announcements', 'Title'), ('Announcements', 'Message'),
                              ('Bookings', 'AdminNote')):
            cur.execute(f'UPDATE {table} SET {column}=REPLACE({column}, ?, ?)', (old, new))

    # Announcement 2 was about a room that is no longer under maintenance.
    cur.execute('''UPDATE Announcements
                      SET Title='New PTTA rooms are now bookable',
                          Message='Makmal A.I. UTHM, Bilik Lestari 1 & 2, Lestari Lounge, Bilik Eksplorasi, Auditorium AI-Jazari, Bilik Hikmah, Bilik Iqra'' and Remote Pilot Smart Classroom.',
                          Kind='Info', IsActive=1
                    WHERE Id=2''')
    cur.execute('''UPDATE Announcements
                      SET Title='Bilik Iqra'' carrel rental',
                          Message='118 reading carrels at ARAS 2 & 4 are rented for one month at RM20.00. Payment is verified at the PTTA counter.',
                          Kind='Info', IsActive=1
                    WHERE Id=3''')

    con.commit()

    print('\nFacilities now:')
    for row in cur.execute('select Id, Code, Name, Type, Capacity, CapacityMin, Units, Location from Facilities order by Type, Code'):
        print('  ', row)
    print('\nBookings per facility:')
    for row in cur.execute('select FacilityId, count(*) from Bookings group by FacilityId'):
        print('  ', row)
    print('\nOrphan checks:')
    print('  bookings w/o facility:', cur.execute('select count(*) from Bookings b left join Facilities f on f.Id=b.FacilityId where f.Id is null').fetchone()[0])
    print('  feedback w/o facility:', cur.execute('select count(*) from Feedbacks fb left join Facilities f on f.Id=fb.FacilityId where fb.FacilityId is not null and f.Id is null').fetchone()[0])
    con.close()


if __name__ == '__main__':
    main()
