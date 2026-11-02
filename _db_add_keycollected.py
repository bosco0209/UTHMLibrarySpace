import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cols = [r[1] for r in con.execute('PRAGMA table_info(Bookings)')]
if 'KeyCollected' not in cols:
    con.execute('ALTER TABLE Bookings ADD COLUMN KeyCollected INTEGER NOT NULL DEFAULT 0')
    con.commit()
print('ok')
con.close()
