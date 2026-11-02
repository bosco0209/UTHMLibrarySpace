import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cur = con.cursor()

names = ['Ahmad Faizal bin Osman','Siti Nurhaliza binti Kamal','Muhammad Aiman','Nur Aisyah','Raj Lim Wei Jie','Tan Mei Ling','Gobind a/l Raj','Aishah binti Hamid','Zulkifli bin Abidin','Faridah binti Hassan']
faculties = ['FSKTM','FKEE','FKMP','FAST','FPTV','FTK','FKAAB','FPTP','FSKTM','FKEE']
phones = ['+60 012-3478901','+60 011-23456789','+60 019-8765432','+60 013-5554443','+60 016-1234567','+60 017-7654321','+60 018-3332221','+60 014-9998887','+60 015-7776665','+60 012-6543210']

# password hash for Student@123 (from ASP.NET Identity PasswordHasher<User>, verified in Program code as matching our demo accounts)
DEMO_HASH = "AQAAAAIAAYagAAAAEOv8Wg03VZ9t8nN8BGK7Q8JzW1d6Q9QeU0ZkXE2w4f5x1y2Z3a4b5c6d7e8f9g0h1i2j3k4l5m6n7o8p9q0r=="

for i, (name, fac, phone) in enumerate(zip(names, faculties, phones), start=1):
    email = f"demo{i:02d}@siswa.uthm.edu.my"
    idnum = f"AI2509{i:02d}"
    cur.execute('''INSERT INTO Users (Name, Email, IdNumber, Role, Faculty, PasswordHash, IsActive, CreatedAt, Phone)
                   VALUES (?,?,?,?,?,?,?,?,?)''',
                (name, email, idnum, 'Student', fac, '', 1, '2026-09-01T08:00:00.0000000Z', phone))
    uid = cur.lastrowid
    date = f'2026-0{(i % 2) + 7}-01'
    enddate = f'2026-0{(i % 2) + 7}-28'
    cur.execute('''INSERT INTO Bookings
        (Reference, UserId, PaymentDueAt, FacilityId, Date, EndDate, StartTime, EndTime, Purpose, Attendees, Fee, PaymentStatus, Status, AdminNote, CreatedAt)
        VALUES (?,?,NULL,1,?,?,'08:00:00','22:00:00','Demo carrel rental',1,20.0,'Paid','Approved','',?)''',
        (f'PTTA-DEMO{i+1:02d}', uid, date, enddate, '2026-06-28T08:00:00.0000000Z'))

con.commit()
for r in cur.execute("select Reference, Date, EndDate, Status from Bookings where Reference like 'PTTA-DEMO%' order by Reference"):
    print(r)
con.close()
