CREATE TABLE apex_clip_detection
(
    id                  uuid NOT NULL DEFAULT (gen_random_uuid()),
    clip_id             uuid NOT NULL,
    task_id             uuid,
    status              int  NOT NULL,
    primary_detection   int  NOT NULL,
    secondary_detection int  NOT NULl,
    CONSTRAINT user_frequent_link_pkey PRIMARY KEY (id),
    CONSTRAINT fk_clip_id__clip FOREIGN KEY (clip_id) REFERENCES clip (id) ON DELETE CASCADE
)