import sqlite3
con = sqlite3.connect(r'C:\Users\user\source\repos\UTHMLibrarySpace\UTHMLibrarySpace\librarysp.db')
cur = con.cursor()
cols = [r[1] for r in cur.execute('PRAGMA table_info(Users)')]
if 'BookingRole' not in cols:
    cur.execute("ALTER TABLE Users ADD COLUMN BookingRole TEXT NOT NULL DEFAULT ''")
    con.commit()
    print('BookingRole column added')
else:
    print('BookingRole column already present')
print([r[1] for r in cur.execute('PRAGMA table_info(Users)')])
con.close()