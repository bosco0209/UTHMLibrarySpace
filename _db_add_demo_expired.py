import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
con.execute('''INSERT INTO Bookings
 (Reference, UserId, PaymentDueAt, FacilityId, Date, EndDate, StartTime, EndTime, Purpose, Attendees, Fee, PaymentStatus, Status, AdminNote, CreatedAt)
 VALUES ('PTTA-DEMO01', 8, NULL, 1, '2026-08-01', '2026-08-31', '08:00:00', '22:00:00', 'Final-year project carrel', 1, 20.0, 'Paid', 'Approved', '', '2026-07-30T08:00:00.0000000Z')''')
con.commit()
for r in con.execute('select Id, Reference, UserId, FacilityId, Status, Date, EndDate from Bookings where Reference="PTTA-DEMO01"'):
    print(r)
con.close()
