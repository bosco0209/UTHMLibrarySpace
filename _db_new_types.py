import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cur = con.cursor()

# Facilities.Type: old category -> new per-room type
for code, new in [
    ('SR-01', 'BilikIqra'),
    ('MR-01', 'LestariLounge'),
    ('MR-02', 'BilikEksplorasi'),
    ('MR-03', 'BilikHikmah'),
    ('TR-01', 'BilikLestari'),
    ('TR-02', 'BilikLestari'),
    ('HL-01', 'Auditorium'),
    ('TC-01', 'MakmalAI'),
    ('TC-02', 'RemotePilot'),
]:
    cur.execute('UPDATE Facilities SET Type=? WHERE Code=?', (new, code))

# CategoryLimits: replace with the new 8 types
cur.execute('DELETE FROM CategoryLimits')
rows = [
    ('BilikIqra', None, None, 1),
    ('LestariLounge', 3, 6, 3),
    ('BilikEksplorasi', 3, 6, 3),
    ('BilikHikmah', 3, 6, 3),
    ('BilikLestari', 4, 12, 5),
    ('Auditorium', 8, 16, 3),
    ('MakmalAI', 4, 12, 4),
    ('RemotePilot', 4, 12, 4),
]
for t, mx, wk, act in rows:
    cur.execute('INSERT INTO CategoryLimits (Type, MaxHoursPerBooking, MaxHoursPerWeek, MaxActiveBookings) VALUES (?,?,?,?)',
                (t, mx, wk, act))

con.commit()
print('Facilities:')
for r in cur.execute('select Code, Name, Type from Facilities order by Code'):
    print(' ', r)
print('CategoryLimits:')
for r in cur.execute('select Type from CategoryLimits order by Type'):
    print(' ', r)
con.close()
